using EMS.Domain.Common;
using EMS.Domain.MasterData;
using EMS.Domain.Shares;

namespace EMS.Domain.Shareholders;

/// <summary>Shareholder master (16.1). Created only from an approved ShareholderApplication (6.5 acceptance criteria).</summary>
public class Shareholder : AuditableEntity
{
    /// <summary>Business reference, e.g. SH-250000001 (Appendix B / KYC-007).</summary>
    public string ShareholderNo { get; set; } = string.Empty;

    public ApplicantType Type { get; set; }
    public long ShareholderGroupId { get; set; }
    public ShareholderGroup? ShareholderGroup { get; set; }
    public ShareholderStatus Status { get; set; } = ShareholderStatus.Active;
    public DateOnly RegistrationDate { get; set; }
    public KycResult KycStatus { get; set; }
    public DateOnly? KycApprovalDate { get; set; }
    public RiskRating RiskRating { get; set; } = RiskRating.Low;

    public long? SourceApplicationId { get; set; }

    public Person? Person { get; set; }
    public Corporate? Corporate { get; set; }

    public ICollection<JointHolder> JointHolders { get; set; } = new List<JointHolder>();
    public ICollection<Address> Addresses { get; set; } = new List<Address>();
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<BankAccount> BankAccounts { get; set; } = new List<BankAccount>();
    public ICollection<RelationshipDeclaration> RelationshipDeclarations { get; set; } = new List<RelationshipDeclaration>();
}

/// <summary>6.2 Personal applicant data captured on the shareholder once approved.</summary>
public class Person
{
    public long PersonId { get; set; }
    public long ShareholderId { get; set; }
    public Shareholder? Shareholder { get; set; }

    public string NameEn { get; set; } = string.Empty;
    public string NameMm { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string FatherName { get; set; } = string.Empty;
    public string NrcPrefixCode { get; set; } = string.Empty;
    public string NrcNumber { get; set; } = string.Empty;
    public MaritalStatus MaritalStatus { get; set; }
    public string? SpouseName { get; set; }
    public string? Qualification { get; set; }
    public string? Specialization { get; set; }

    public string? WorkNature { get; set; }
    public string? Position { get; set; }
    public int? YearsOfService { get; set; }
    public string? CompanyAddress { get; set; }
}

/// <summary>6.3 Corporate applicant data.</summary>
public class Corporate
{
    public long CorporateId { get; set; }
    public long ShareholderId { get; set; }
    public Shareholder? Shareholder { get; set; }

    public string LegalNameEn { get; set; } = string.Empty;
    public string LegalNameMm { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public DateOnly RegistrationDate { get; set; }
    public string LegalForm { get; set; } = string.Empty;
    public string? TaxIdentifier { get; set; }
    public string ContactPersonName { get; set; } = string.Empty;

    public ICollection<CorporateSignatory> Signatories { get; set; } = new List<CorporateSignatory>();
    public ICollection<BeneficialOwner> BeneficialOwners { get; set; } = new List<BeneficialOwner>();
}

public class CorporateSignatory
{
    public long CorporateSignatoryId { get; set; }
    public long CorporateId { get; set; }
    public Corporate? Corporate { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public bool IsAuthorizedSigner { get; set; }
    public bool IsDirector { get; set; }
}

/// <summary>6.3 beneficial owner declaration - total percentages must equal 100% when required.</summary>
public class BeneficialOwner
{
    public long BeneficialOwnerId { get; set; }
    public long CorporateId { get; set; }
    public Corporate? Corporate { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NrcOrRegistrationNumber { get; set; } = string.Empty;
    public decimal OwnershipPercentage { get; set; }
}

/// <summary>6.3 Joint application - two or more linked persons with ownership percentage.</summary>
public class JointHolder
{
    public long JointHolderId { get; set; }
    public long ShareholderId { get; set; }
    public Shareholder? Shareholder { get; set; }

    public string NameEn { get; set; } = string.Empty;
    public string NameMm { get; set; } = string.Empty;
    public string NrcNumber { get; set; } = string.Empty;
    public decimal OwnershipPercentage { get; set; }
    public bool IsPrimaryContact { get; set; }
    public string JointOperatingInstruction { get; set; } = string.Empty;
}

public class Address
{
    public long AddressId { get; set; }
    public long ShareholderId { get; set; }
    public Shareholder? Shareholder { get; set; }

    public AddressType AddressType { get; set; }
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string Township { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string StateRegion { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
}

public class Contact
{
    public long ContactId { get; set; }
    public long ShareholderId { get; set; }
    public Shareholder? Shareholder { get; set; }

    public ContactType ContactType { get; set; }
    public string Value { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

/// <summary>6.2 Bank Reference - CB account and other-bank details.</summary>
public class BankAccount
{
    public long BankAccountId { get; set; }
    public long ShareholderId { get; set; }
    public Shareholder? Shareholder { get; set; }

    public bool IsCbBank { get; set; }
    public long? BranchId { get; set; }
    public BankBranch? Branch { get; set; }
    public string? OtherBankName { get; set; }
    public string? OtherBranchName { get; set; }

    /// <summary>Masked value shown in list screens (11.4); full value stored encrypted at rest.</summary>
    public string AccountNumberMasked { get; set; } = string.Empty;
    public string AccountNumberEncrypted { get; set; } = string.Empty;
}

/// <summary>6.2 Relationships - relatives, controlled accounts, related-party declarations.</summary>
public class RelationshipDeclaration
{
    public long DeclarationId { get; set; }
    public long ShareholderId { get; set; }
    public Shareholder? Shareholder { get; set; }

    public string DeclarationType { get; set; } = string.Empty;
    public bool Answer { get; set; }
    public string? Details { get; set; }
}
