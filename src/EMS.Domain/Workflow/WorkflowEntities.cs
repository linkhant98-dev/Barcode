using EMS.Domain.Common;

namespace EMS.Domain.Workflow;

/// <summary>
/// Section 15.2 / 5 - configurable approval matrix: transaction type, group/class, amount range, sequence, role.
/// Effective-dated and versioned; must not have gaps or duplicate sequence numbers for the same route key (5).
/// </summary>
public class ApprovalMatrixRule : AuditableEntity
{
    public string Module { get; set; } = string.Empty; // SA, IS, TS, BS, DS
    public long? ShareholderGroupId { get; set; }
    public long? ShareClassId { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }

    public int Sequence { get; set; }
    public string StepName { get; set; } = string.Empty; // e.g. DGM, Legal, DMD, Managing Director, CEO, Vice Chairman, Board of Directors, CBM
    public string ApproverRole { get; set; } = string.Empty;
    public bool IsMandatory { get; set; } = true;
    public ApprovalStageMode StageMode { get; set; } = ApprovalStageMode.Sequential;
    public bool IsConditional { get; set; } // e.g. CBM step enabled only when conditions match (11.1)

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Section 11.1 - one approval route instance per submitted entity. The route is snapshotted at submission time
/// so later matrix changes do not retroactively alter an in-flight approval (Snapshot rule).
/// </summary>
public class ApprovalInstance : AuditableEntity
{
    public string EntityType { get; set; } = string.Empty; // ShareholderApplication, ShareTransaction, BonusEvent, DividendEvent
    public long EntityId { get; set; }
    public string EntityReference { get; set; } = string.Empty;
    public int RouteVersion { get; set; } = 1;
    public WorkflowStatus Status { get; set; } = WorkflowStatus.PendingApproval;
    public string SubmittedByUserId { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }
    public int CurrentSequence { get; set; }

    public ICollection<ApprovalStep> Steps { get; set; } = new List<ApprovalStep>();
}

/// <summary>One snapshotted step in the approval route with its decision history (13.1 approval timeline).</summary>
public class ApprovalStep : AuditableEntity
{
    public long ApprovalInstanceId { get; set; }
    public ApprovalInstance? ApprovalInstance { get; set; }

    public int Sequence { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string ApproverRole { get; set; } = string.Empty;
    public string? AssignedUserId { get; set; }
    public bool IsMandatory { get; set; } = true;
    public ApprovalStepStatus Status { get; set; } = ApprovalStepStatus.Waiting;

    public ApprovalDecision? Decision { get; set; }
    public string? DecisionByUserId { get; set; }
    public DateTime? DecisionAtUtc { get; set; }
    public string? Comment { get; set; }
    public string? DelegatedToUserId { get; set; }
}

/// <summary>Temporary delegation with dates and reason; no self-delegation (11.1).</summary>
public class Delegation : AuditableEntity
{
    public string FromUserId { get; set; } = string.Empty;
    public string ToUserId { get; set; } = string.Empty;
    public string? ApproverRole { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
