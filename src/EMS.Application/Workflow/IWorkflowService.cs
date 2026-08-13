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
    long EntityVersionSeenByApprover);

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
    /// Applies Approve/Reject/Revert/RequestInformation/Delegate to the current pending step, enforcing
    /// maker-checker (COM-011) and mandatory-comment rules for Reject/Revert (11.1).
    /// </summary>
    Task<WorkflowResult> DecideAsync(ApprovalDecisionRequest request, CancellationToken ct = default);

    Task<ApprovalInstance?> GetActiveInstanceAsync(string entityType, long entityId, CancellationToken ct = default);
}
