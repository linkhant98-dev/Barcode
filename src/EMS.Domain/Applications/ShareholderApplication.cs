using EMS.Domain.Common;
using EMS.Domain.MasterData;

namespace EMS.Domain.Applications;

/// <summary>Section 6 - Shareholder Application (SA). One approved application produces exactly one Shareholder (6.5).</summary>
public class ShareholderApplication : AuditableEntity
{
    /// <summary>Business reference, e.g. SA-2026-000001 (Appendix B).</summary>
    public string ApplicationNo { get; set; } = string.Empty;
    public ApplicantType Type { get; set; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public DateOnly? SubmittedDate { get; set; }
    public string MakerUserId { get; set; } = string.Empty;

    public long ShareholderGroupId { get; set; }
    public ShareholderGroup? ShareholderGroup { get; set; }

    // Personal fields (6.2)
    public string? NameEn { get; set; }
    public string? NameMm { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? FatherName { get; set; }
    public string? NrcPrefixCode { get; set; }
    public string? NrcNumber { get; set; }
    public string? MaritalStatus { get; set; }
    public string? SpouseName { get; set; }

    // Corporate fields (6.3)
    public string? LegalNameEn { get; set; }
    public string? RegistrationNumber { get; set; }
    public DateOnly? CorporateRegistrationDate { get; set; }
    public string? LegalForm { get; set; }
    public string? TaxIdentifier { get; set; }

    // Address / contact snapshot captured at application time
    public string? AddressLine1 { get; set; }
    public string? Township { get; set; }
    public string? City { get; set; }
    public string? StateRegion { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }

    public long? CreatedShareholderId { get; set; }

    public KycCase? KycCase { get; set; }
    public ICollection<ApplicationJointHolder> JointHolders { get; set; } = new List<ApplicationJointHolder>();
}

/// <summary>Joint applicant rows captured on the application before the shareholder is registered (6.3).</summary>
public class ApplicationJointHolder
{
    public long ApplicationJointHolderId { get; set; }
    public long ShareholderApplicationId { get; set; }
    public ShareholderApplication? ShareholderApplication { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NrcNumber { get; set; } = string.Empty;
    public decimal OwnershipPercentage { get; set; }
    public bool IsPrimaryContact { get; set; }
}

/// <summary>Section 6.4 KYC case attached 1:1 to an application.</summary>
public class KycCase : AuditableEntity
{
    public long ShareholderApplicationId { get; set; }
    public ShareholderApplication? ShareholderApplication { get; set; }

    public DateTime RequestedAtUtc { get; set; }
    public string RequestedByUserId { get; set; } = string.Empty;
    public KycResult Result { get; set; } = KycResult.Pending;
    public RiskRating RiskRating { get; set; } = RiskRating.Low;
    public string? ScreeningReference { get; set; }
    public DateTime? ScreeningTimestampUtc { get; set; }
    public string? DecisionUserId { get; set; }
    public DateTime? DecisionAtUtc { get; set; }
    public string? DecisionRemark { get; set; }
    public DateOnly? NextReviewDate { get; set; }
}
