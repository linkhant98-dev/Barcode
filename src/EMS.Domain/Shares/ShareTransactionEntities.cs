using EMS.Domain.Common;
using EMS.Domain.MasterData;
using EMS.Domain.Shareholders;

namespace EMS.Domain.Shares;

/// <summary>
/// Common transaction envelope shared by Issue Shares (IS) and Transfer Shares (TS) (16.1 ShareTransaction).
/// Bonus and Dividend events use their own aggregates (CorporateActions) because they are batch/versioned, not single postings.
/// </summary>
public class ShareTransaction : AuditableEntity
{
    /// <summary>Business reference, e.g. IS-2026-000001 / TS-2026-000001 (Appendix B).</summary>
    public string TransactionNo { get; set; } = string.Empty;
    public ShareTransactionType Type { get; set; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public DateOnly EffectiveDate { get; set; }
    public string MakerUserId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }

    public ShareIssue? ShareIssue { get; set; }
    public ShareTransfer? ShareTransfer { get; set; }

    public long? ApprovalInstanceId { get; set; }
}

/// <summary>Section 7 - Issue Shares main fields, calculated amounts and payment.</summary>
public class ShareIssue
{
    public long TransactionId { get; set; }
    public ShareTransaction? Transaction { get; set; }

    public IssueApplyType ApplyType { get; set; }
    public long ShareholderId { get; set; }
    public Shareholder? Shareholder { get; set; }
    public long ShareClassId { get; set; }
    public ShareClass? ShareClass { get; set; }

    public decimal NumberOfShares { get; set; }
    public decimal CapitalValuePerShare { get; set; }
    public decimal PremiumValuePerShare { get; set; }

    // IS-BR-03: system-calculated, read-only
    public decimal CapitalAmount { get; set; }
    public decimal PremiumAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public decimal CashAmount { get; set; }
    public decimal ChequeAmount { get; set; }
    public string? ChequeNumber { get; set; }
    public DateOnly? ChequeDate { get; set; }
    public long? BankBranchId { get; set; }
    public string? AccountTransferReference { get; set; }

    public long? DividendEntitlementId { get; set; }

    public string? CertificateRangeFrom { get; set; }
    public string? CertificateRangeTo { get; set; }
}

/// <summary>Section 8 - Transfer Shares main fields and conditional logic.</summary>
public class ShareTransfer
{
    public long TransactionId { get; set; }
    public ShareTransaction? Transaction { get; set; }

    public long FromShareholderId { get; set; }
    public Shareholder? FromShareholder { get; set; }
    public long ToShareholderId { get; set; }
    public Shareholder? ToShareholder { get; set; }

    public long ShareClassId { get; set; }
    public ShareClass? ShareClass { get; set; }
    public decimal Quantity { get; set; }

    public TransferType TransferType { get; set; }

    // Trade transfer value fields
    public decimal CapitalAmount { get; set; }
    public decimal PremiumAmount { get; set; }
    public decimal TotalConsideration { get; set; }
    public decimal CashAmount { get; set; }
    public decimal ChequeAmount { get; set; }

    // Non-trade transfer
    public NonTradeReason? NonTradeReason { get; set; }
    public string? NonTradeReasonDetail { get; set; }

    public string? CancelledCertificateNumbers { get; set; }
    public string? NewCertificateNumbers { get; set; }
}

/// <summary>16.1 ShareLedger - immutable posted debit/credit entries; the single source of truth for balances (13.1.1).</summary>
public class ShareLedgerEntry
{
    public long LedgerId { get; set; }
    public long ShareholderId { get; set; }
    public Shareholder? Shareholder { get; set; }
    public long ShareClassId { get; set; }
    public ShareClass? ShareClass { get; set; }

    public long? SourceTransactionId { get; set; }
    public long? SourceBonusEventId { get; set; }
    public long? SourceDividendEventId { get; set; }
    public string SourceReference { get; set; } = string.Empty;

    /// <summary>Positive for credit (increase), negative for debit (decrease). Never reversed in place - see IS-BR-09.</summary>
    public decimal QuantityDelta { get; set; }
    public decimal CapitalAmountDelta { get; set; }
    public decimal PremiumAmountDelta { get; set; }
    public decimal RunningQuantityBalance { get; set; }
    public decimal RunningPaidUpCapital { get; set; }

    public DateOnly EffectiveDate { get; set; }
    public DateTime PostedAtUtc { get; set; }
    public string PostedByUserId { get; set; } = string.Empty;
    public bool IsReversed { get; set; }
    public long? ReversalOfLedgerId { get; set; }
}

/// <summary>16.1 ShareCertificate - full certificate lifecycle (RPT-003).</summary>
public class ShareCertificate : AuditableEntity
{
    public string CertificateNumber { get; set; } = string.Empty;
    public long ShareholderId { get; set; }
    public Shareholder? Shareholder { get; set; }
    public long ShareClassId { get; set; }
    public ShareClass? ShareClass { get; set; }
    public decimal Quantity { get; set; }
    public CertificateStatus Status { get; set; } = CertificateStatus.PendingPrint;

    public long? IssueTransactionId { get; set; }
    public DateOnly IssueDate { get; set; }
    public string? StartSerialNumber { get; set; }
    public string? EndSerialNumber { get; set; }

    public DateOnly? PrintedDate { get; set; }
    public string? PrintedByUserId { get; set; }
    public DateOnly? DeliveredDate { get; set; }
    public string? DeliveredByUserId { get; set; }

    public DateOnly? CancelledDate { get; set; }
    public long? ReplacementCertificateId { get; set; }
    public string? CancellationReason { get; set; }
}
