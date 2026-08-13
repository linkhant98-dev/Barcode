using EMS.Domain.Common;
using EMS.Domain.Shareholders;

namespace EMS.Domain.CorporateActions;

/// <summary>Section 10 - Dividend Management batch/versioned event.</summary>
public class DividendEvent : AuditableEntity
{
    /// <summary>Business reference, e.g. DS-2026-000001.</summary>
    public string DividendEventNo { get; set; } = string.Empty;
    public string FinancialYear { get; set; } = string.Empty;
    public DateOnly RecordDate { get; set; }
    public decimal DividendPercentage { get; set; }
    public decimal CapitalValuePerShare { get; set; }

    /// <summary>e.g. DividendShare-YYYYMMDD-v1.0 (10.3).</summary>
    public string BatchVersion { get; set; } = string.Empty;
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public string MakerUserId { get; set; } = string.Empty;

    public long? CalculationFileAttachmentId { get; set; }
    public long? ApprovalInstanceId { get; set; }
    public DateOnly? PostedDate { get; set; }

    public ICollection<DividendEntitlement> Entitlements { get; set; } = new List<DividendEntitlement>();
}

/// <summary>Per-shareholder dividend calculation - old/new share split with prorated new-share dividend (10.1).</summary>
public class DividendEntitlement : AuditableEntity
{
    public long DividendEventId { get; set; }
    public DividendEvent? DividendEvent { get; set; }
    public long ShareholderId { get; set; }
    public Shareholder? Shareholder { get; set; }

    public decimal OldShares { get; set; }
    public decimal NewShares { get; set; }
    public int EligibleDaysForNewShares { get; set; }

    public decimal OldShareDividend { get; set; }
    public decimal NewShareDividend { get; set; }
    public decimal TotalDividend { get; set; }

    public decimal CashWithdrawal { get; set; }
    public decimal AccountTransfer { get; set; }
    public decimal ReinvestedAmount { get; set; }
    public decimal AdjustmentAmount { get; set; }
    public decimal OutstandingBalance { get; set; }

    public ICollection<DividendSettlement> Settlements { get; set; } = new List<DividendSettlement>();
}

/// <summary>10.2 Settlement options - cash, CB/other bank transfer, reinvestment, or a combination.</summary>
public class DividendSettlement : AuditableEntity
{
    public long DividendEntitlementId { get; set; }
    public DividendEntitlement? DividendEntitlement { get; set; }

    public DividendSettlementMethod Method { get; set; }
    public decimal Amount { get; set; }
    public DateOnly SettlementDate { get; set; }
    public string? Reference { get; set; }
    public string Status { get; set; } = "Completed";

    /// <summary>Set when Method = Reinvestment and this settlement created a linked Issue Shares transaction (10.3).</summary>
    public long? LinkedShareTransactionId { get; set; }
    public bool IsRolledBack { get; set; }
}
