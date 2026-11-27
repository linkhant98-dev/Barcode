using EMS.Domain.Common;
using EMS.Domain.Shareholders;
using EMS.Domain.Shares;

namespace EMS.Domain.CorporateActions;

/// <summary>Section 9 - Bonus Shares batch/versioned event. Ratio and cash rate are parameters, never hard-coded (9.1).</summary>
public class BonusEvent : AuditableEntity
{
    /// <summary>Business reference, e.g. BS-2026-000001.</summary>
    public string BonusEventNo { get; set; } = string.Empty;
    public string FinancialYear { get; set; } = string.Empty;
    public DateOnly RecordDate { get; set; }
    public int BonusNumerator { get; set; }
    public int BonusDenominator { get; set; }
    public decimal CashBonusRatePerRemainderShare { get; set; }
    public long ShareClassId { get; set; }

    /// <summary>e.g. BonusShare-YYYYMMDD-v1.0 (9.3).</summary>
    public string BatchVersion { get; set; } = string.Empty;
    public string RoundingMethod { get; set; } = "Floor";
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public string MakerUserId { get; set; } = string.Empty;

    public long? CalculationFileAttachmentId { get; set; }
    public long? ApprovalInstanceId { get; set; }
    public DateOnly? PostedDate { get; set; }

    public ICollection<BonusEntitlement> Entitlements { get; set; } = new List<BonusEntitlement>();
}

/// <summary>Per-shareholder calculation snapshot retained to reproduce results (9.3).</summary>
public class BonusEntitlement
{
    public long BonusEntitlementId { get; set; }
    public long BonusEventId { get; set; }
    public BonusEvent? BonusEvent { get; set; }
    public long ShareholderId { get; set; }
    public Shareholder? Shareholder { get; set; }

    public decimal EligibleShares { get; set; }
    public decimal RawBonusEntitlement { get; set; }
    public decimal BonusShares { get; set; }
    public decimal RemainderShares { get; set; }
    public decimal CashBonusAmount { get; set; }
    public decimal NewTotalShares { get; set; }

    public bool IsPosted { get; set; }
    public string? SettlementStatus { get; set; }
}
