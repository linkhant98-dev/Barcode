using EMS.Application.Abstractions;
using EMS.Application.Workflow;
using EMS.Domain.Common;
using EMS.Domain.Workflow;
using EMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Services;

/// <summary>
/// Section 11 - generic configurable approval engine used by every transaction type. The route is resolved
/// from ApprovalMatrixRule and snapshotted into ApprovalStep rows so later matrix edits never change an
/// in-flight approval (11.1 Snapshot). Maker-checker (COM-011) blocks the submitter from deciding their own item.
/// </summary>
public class WorkflowService : IWorkflowService
{
    private readonly EmsDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;

    public WorkflowService(EmsDbContext db, ICurrentUserService currentUser, IAuditService audit, INotificationService notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _notifications = notifications;
    }

    public async Task<ApprovalInstance> SubmitForApprovalAsync(SubmitForApprovalRequest request, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rules = await _db.ApprovalMatrixRules
            .Where(r => r.IsActive
                && r.Module == request.Module
                && r.EffectiveFrom <= today
                && (r.EffectiveTo == null || r.EffectiveTo >= today)
                && (r.ShareholderGroupId == null || r.ShareholderGroupId == request.ShareholderGroupId)
                && (r.ShareClassId == null || r.ShareClassId == request.ShareClassId)
                && (r.MinAmount == null || request.Amount == null || request.Amount >= r.MinAmount)
                && (r.MaxAmount == null || request.Amount == null || request.Amount <= r.MaxAmount))
            .OrderBy(r => r.Sequence)
            .ToListAsync(ct);

        if (rules.Count == 0)
        {
            // No specific route configured - fall back to the module's default (group/class/amount-agnostic) rules.
            rules = await _db.ApprovalMatrixRules
                .Where(r => r.IsActive && r.Module == request.Module
                    && r.ShareholderGroupId == null && r.ShareClassId == null
                    && r.MinAmount == null && r.MaxAmount == null)
                .OrderBy(r => r.Sequence)
                .ToListAsync(ct);
        }

        var instance = new ApprovalInstance
        {
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            EntityReference = request.EntityReference,
            RouteVersion = 1,
            Status = WorkflowStatus.PendingApproval,
            SubmittedByUserId = _currentUser.UserId,
            SubmittedAtUtc = DateTime.UtcNow,
            CurrentSequence = rules.Count > 0 ? rules[0].Sequence : 0,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        foreach (var rule in rules)
        {
            instance.Steps.Add(new ApprovalStep
            {
                Sequence = rule.Sequence,
                StepName = rule.StepName,
                ApproverRole = rule.ApproverRole,
                IsMandatory = rule.IsMandatory,
                Status = rule.Sequence == instance.CurrentSequence ? ApprovalStepStatus.Pending : ApprovalStepStatus.Waiting,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId
            });
        }

        _db.ApprovalInstances.Add(instance);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Submit", request.Module, request.EntityType, request.EntityReference, ct: ct);

        var firstStep = instance.Steps.FirstOrDefault(s => s.Status == ApprovalStepStatus.Pending);
        if (firstStep is not null)
        {
            await _notifications.NotifyRoleAsync(firstStep.ApproverRole, "APPROVAL_ASSIGNED",
                $"Approval needed - {instance.EntityReference}", $"{instance.EntityReference} is awaiting your {firstStep.StepName} decision.",
                instance.EntityReference, ApprovalReviewLink(instance), ct);
        }

        return instance;
    }

    public async Task<WorkflowResult> DecideAsync(ApprovalDecisionRequest request, CancellationToken ct = default)
    {
        var step = await _db.ApprovalSteps
            .Include(s => s.ApprovalInstance)!.ThenInclude(i => i!.Steps)
            .FirstOrDefaultAsync(s => s.Id == request.ApprovalStepId, ct);

        if (step is null || step.ApprovalInstance is null)
            return new WorkflowResult(false, WorkflowStatus.PendingApproval, "Approval step not found.");

        var instance = step.ApprovalInstance;

        if (instance.Status != WorkflowStatus.PendingApproval || step.Status != ApprovalStepStatus.Pending)
            return new WorkflowResult(false, instance.Status, "This step is not currently awaiting a decision.");

        // COM-011 maker-checker: the submitter cannot approve their own item.
        if (instance.SubmittedByUserId == _currentUser.UserId)
            return new WorkflowResult(false, instance.Status, "The maker cannot approve, reject or revert their own submission.");

        // 11.1 - only a user holding the step's approver role (or an active delegate of that role) may decide it.
        if (!await IsAuthorizedForStepAsync(step, ct))
            return new WorkflowResult(false, instance.Status, $"Only a {step.ApproverRole} (or their delegate) can decide this step.");

        // 11.1 - comment is mandatory for Reject/Revert.
        if ((request.Decision is ApprovalDecision.Reject or ApprovalDecision.Revert) && string.IsNullOrWhiteSpace(request.Comment))
            return new WorkflowResult(false, instance.Status, "A comment is required for Reject or Revert.");

        step.Decision = request.Decision;
        step.DecisionByUserId = _currentUser.UserId;
        step.DecisionAtUtc = DateTime.UtcNow;
        step.Comment = request.Comment;
        step.ModifiedAtUtc = DateTime.UtcNow;
        step.ModifiedBy = _currentUser.UserId;

        switch (request.Decision)
        {
            case ApprovalDecision.Reject:
                step.Status = ApprovalStepStatus.Rejected;
                instance.Status = WorkflowStatus.Rejected;
                break;

            case ApprovalDecision.Revert:
                step.Status = ApprovalStepStatus.Reverted;
                instance.Status = WorkflowStatus.Reverted;
                break;

            case ApprovalDecision.RequestInformation:
                instance.Status = WorkflowStatus.Reverted;
                step.Status = ApprovalStepStatus.Reverted;
                break;

            case ApprovalDecision.Delegate:
                step.DelegatedToUserId = request.Comment; // caller passes target user id via Comment for simplicity
                step.Status = ApprovalStepStatus.Pending;
                break;

            case ApprovalDecision.Approve:
            default:
                step.Status = ApprovalStepStatus.Approved;
                var remainingSteps = instance.Steps.Where(s => s.Sequence > step.Sequence).OrderBy(s => s.Sequence).ToList();
                var nextStep = remainingSteps.FirstOrDefault();
                if (nextStep is null)
                {
                    instance.Status = WorkflowStatus.Approved;
                }
                else
                {
                    nextStep.Status = ApprovalStepStatus.Pending;
                    instance.CurrentSequence = nextStep.Sequence;
                    instance.Status = WorkflowStatus.PendingApproval;
                }
                break;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Decision", instance.EntityType, instance.EntityType, instance.EntityReference,
            after: new { request.Decision, request.Comment }, ct: ct);

        await NotifyAfterDecisionAsync(instance, step, request.Decision, ct);

        return new WorkflowResult(true, instance.Status);
    }

    public Task<ApprovalInstance?> GetActiveInstanceAsync(string entityType, long entityId, CancellationToken ct = default) =>
        _db.ApprovalInstances
            .Include(i => i.Steps)
            .Where(i => i.EntityType == entityType && i.EntityId == entityId)
            .OrderByDescending(i => i.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Section 11.1 - a step can be decided by a user holding the step's ApproverRole directly, or by anyone
    /// an approver has actively delegated to for that role (Delegation, dated, no self-delegation enforced at
    /// creation time in DelegationService).
    /// </summary>
    private async Task<bool> IsAuthorizedForStepAsync(ApprovalStep step, CancellationToken ct)
    {
        if (_currentUser.IsInRole(step.ApproverRole))
            return true;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _db.Delegations.AnyAsync(d =>
            d.IsActive
            && d.ToUserId == _currentUser.UserId
            && d.ApproverRole == step.ApproverRole
            && d.StartDate <= today
            && d.EndDate >= today, ct);
    }

    /// <summary>Section 14 - notify the next approver on a non-final approval, or the maker once the route settles.</summary>
    private async Task NotifyAfterDecisionAsync(ApprovalInstance instance, ApprovalStep decidedStep, ApprovalDecision decision, CancellationToken ct)
    {
        switch (decision)
        {
            case ApprovalDecision.Approve when instance.Status == WorkflowStatus.PendingApproval:
                var nextStep = instance.Steps.FirstOrDefault(s => s.Status == ApprovalStepStatus.Pending);
                if (nextStep is not null)
                {
                    await _notifications.NotifyRoleAsync(nextStep.ApproverRole, "APPROVAL_ASSIGNED",
                        $"Approval needed - {instance.EntityReference}", $"{instance.EntityReference} is awaiting your {nextStep.StepName} decision.",
                        instance.EntityReference, ApprovalReviewLink(nextStep), ct);
                }
                break;

            case ApprovalDecision.Approve:
                await _notifications.NotifyUserAsync(instance.SubmittedByUserId, "APPROVAL_APPROVED",
                    $"Approved - {instance.EntityReference}", $"{instance.EntityReference} has completed approval.",
                    instance.EntityReference, ct: ct);
                break;

            case ApprovalDecision.Reject:
            case ApprovalDecision.Revert:
            case ApprovalDecision.RequestInformation:
                await _notifications.NotifyUserAsync(instance.SubmittedByUserId, "APPROVAL_" + decision.ToString().ToUpperInvariant(),
                    $"{decision} - {instance.EntityReference}", $"{instance.EntityReference} was {decision.ToString().ToLowerInvariant()}ed by {decidedStep.StepName}: {decidedStep.Comment}",
                    instance.EntityReference, ct: ct);
                break;
        }
    }

    private static string ApprovalReviewLink(ApprovalInstance instance)
    {
        var pendingStep = instance.Steps.FirstOrDefault(s => s.Status == ApprovalStepStatus.Pending);
        return pendingStep is null ? "/Approvals" : $"/Approvals/Review/{pendingStep.Id}";
    }

    private static string ApprovalReviewLink(ApprovalStep step) => $"/Approvals/Review/{step.Id}";
}
