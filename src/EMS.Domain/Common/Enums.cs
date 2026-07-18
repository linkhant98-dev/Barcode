namespace EMS.Domain.Common;

/// <summary>Appendix A - Status Codes, applied to applications, transactions and approval-bearing records.</summary>
public enum WorkflowStatus
{
    Draft,
    Submitted,
    KycPending,
    KycApproved,
    KycRejected,
    PendingApproval,
    Reverted,
    Approved,
    Rejected,
    Processing,
    PostingError,
    Completed,
    Cancelled,
    Archived
}

public enum ApplicantType
{
    Personal,
    Joint,
    Corporate
}

public enum ShareholderStatus
{
    Active,
    Suspended,
    Closed
}

public enum KycResult
{
    Pending,
    PendingAdditionalInformation,
    Approved,
    Rejected,
    Reverted,
    Expired
}

public enum RiskRating
{
    Low,
    Medium,
    High,
    Prohibited
}

public enum Gender
{
    Male,
    Female,
    Other
}

public enum MaritalStatus
{
    Single,
    Married,
    Divorced,
    Widowed
}

public enum AddressType
{
    Permanent,
    Current,
    Registered,
    Business
}

public enum ContactType
{
    Mobile,
    Email,
    Phone
}

/// <summary>COM-003 transaction types drive reference-number prefixes (Appendix B).</summary>
public enum ShareTransactionType
{
    IssueShares,
    TransferShares,
    BonusShares,
    DividendShares
}

public enum IssueApplyType
{
    IssueShare,
    DividendReinvest
}

public enum TransferType
{
    Trade,
    NonTrade
}

public enum NonTradeReason
{
    Inheritance,
    Gift,
    CourtOrder,
    Restructuring,
    Other
}

public enum CertificateStatus
{
    Active,
    PendingPrint,
    Printed,
    Delivered,
    Cancelled,
    Replaced,
    Uncollected
}

public enum DividendSettlementMethod
{
    CashWithdrawal,
    CbBankAccountTransfer,
    OtherBankAccountTransfer,
    Reinvestment
}

/// <summary>Section 11 - Approve, Reject, Revert, Request Information, Delegate.</summary>
public enum ApprovalDecision
{
    Approve,
    Reject,
    Revert,
    RequestInformation,
    Delegate
}

public enum ApprovalStepStatus
{
    Waiting,
    Pending,
    Approved,
    Rejected,
    Reverted,
    Skipped
}

public enum ApprovalStageMode
{
    Sequential,
    ParallelAll,
    ParallelAny
}

public enum AttachmentOwnerType
{
    ShareholderApplication,
    Shareholder,
    ShareTransaction,
    BonusEvent,
    DividendEvent,
    ApprovalStep
}

public enum NotificationChannel
{
    InApp,
    Email,
    Sms
}

public enum NotificationStatus
{
    Queued,
    Sent,
    Failed
}

public enum ReportExecutionStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
    Expired
}

public enum ReportOutputFormat
{
    OnScreen,
    Excel,
    Pdf,
    Csv
}

public enum UserStatus
{
    Pending,
    Active,
    Locked,
    Disabled,
    Expired
}
