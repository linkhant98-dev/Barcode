using EMS.Domain.Common;

namespace EMS.Domain.MasterData;

/// <summary>Section 5 - master data. Deactivation/effective-dating is used instead of deletion (5.1).</summary>
public class NrcPrefix : MasterDataEntity
{
    public string StateRegion { get; set; } = string.Empty;
    public string TownshipCode { get; set; } = string.Empty;
    public string CitizenshipType { get; set; } = string.Empty;
}

public class Geography : MasterDataEntity
{
    public string Country { get; set; } = "Myanmar";
    public string StateRegion { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Township { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
}

public class BankBranch : MasterDataEntity
{
    public string Address { get; set; } = string.Empty;
}

public class Department : MasterDataEntity
{
    public long? ParentDepartmentId { get; set; }
    public Department? ParentDepartment { get; set; }
}

public class ShareholderGroup : MasterDataEntity
{
}

/// <summary>Ordinary, preference, founder, common, bonus and future share classes (5, effective-dated).</summary>
public class ShareClass : MasterDataEntity
{
    public bool CarriesCertificate { get; set; } = true;
}

public class DocumentType : MasterDataEntity
{
    public string AllowedExtensions { get; set; } = ".pdf,.docx,.xlsx,.jpg,.jpeg,.png";
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
    public bool IsMandatoryForKyc { get; set; }
}

/// <summary>Reject/revert/cancel/adjustment reasons - bilingual and active-dated (5).</summary>
public class ReasonCode : MasterDataEntity
{
    public string Category { get; set; } = string.Empty;
}

/// <summary>Versioned dividend parameters per financial year (5, section 10.1).</summary>
public class DividendParameter : AuditableEntity
{
    public string FinancialYear { get; set; } = string.Empty;
    public decimal DividendPercentage { get; set; }
    public decimal CapitalValuePerShare { get; set; }
    public DateOnly RecordDate { get; set; }
    public bool IsApproved { get; set; }
}

/// <summary>Versioned bonus parameters (5, section 9.1) - ratio and cash bonus rate are configurable, not hard-coded.</summary>
public class BonusParameter : AuditableEntity
{
    public string FinancialYear { get; set; } = string.Empty;
    public int BonusNumerator { get; set; }
    public int BonusDenominator { get; set; }
    public decimal CashBonusRatePerRemainderShare { get; set; }
    public DateOnly RecordDate { get; set; }
    public bool IsApproved { get; set; }
}
