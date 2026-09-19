using EMS.Domain.Common;
using EMS.Domain.Workflow;

namespace EMS.Application.Workflow;

public record SubmitForApprovalRequest(
    string EntityType,
    long EntityId,
    string EntityReference,
    string Module,
    long? ShareholderGroupId,
    long? ShareClassId,
    decimal? Amount);

public record ApprovalDecisionRequest(
    long ApprovalStepId,
    ApprovalDecision Decision,
    string? Comment,
    long EntityVersionSeenByApprover,
    /// <summary>Required when Decision is CaseTransfer - the approver role the pending step is reassigned to.</summary>
    string? TransferToRole = null);

public record WorkflowResult(bool Success, WorkflowStatus NewStatus, string? Error = null);

/// <summary>
/// Section 11 - configurable, snapshotted, sequential (or parallel) approval engine shared by every
/// transaction type (SA, IS, TS, BS, DS). Route is resolved from ApprovalMatrixRule at submission time
/// and never changes retroactively (Snapshot rule, 11.1).
/// </summary>
public interface IWorkflowService
{
    /// <summary>Resolves the applicable approval matrix and creates a snapshotted ApprovalInstance + Steps.</summary>
    Task<ApprovalInstance> SubmitForApprovalAsync(SubmitForApprovalRequest request, CancellationToken ct = default);

    /// <summary>
    /// Applies Approve/Reject/Revert/RequestInformation/Delegate/CaseTransfer to the current pending step,
    /// enforcing maker-checker (COM-011) and mandatory-comment rules for Reject/Revert (11.1).
    /// </summary>
    Task<WorkflowResult> DecideAsync(ApprovalDecisionRequest request, CancellationToken ct = default);

    /// <summary>
    /// 3.4 "First Approver" Recall action - only the original submitter may call this. Pulls the current
    /// pending step back so its previously-assigned approver can no longer act on it, and (when a role is
    /// supplied) immediately reassigns it to the correct approver role in the same motion.
    /// </summary>
    Task<WorkflowResult> RecallAsync(long approvalInstanceId, string? reassignToRole, string? comment, CancellationToken ct = default);

    Task<ApprovalInstance?> GetActiveInstanceAsync(string entityType, long entityId, CancellationToken ct = default);
}
