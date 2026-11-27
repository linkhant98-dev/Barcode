namespace EMS.Domain.Common;

/// <summary>Base class for aggregate roots that require optimistic concurrency and audit stamps (COM-008, COM-009, DB standards 16.2).</summary>
public abstract class AuditableEntity
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    /// <summary>SQL Server rowversion column mapped for optimistic concurrency.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>Base class for effective-dated master data (section 5, 5.1: deactivate instead of delete).</summary>
public abstract class MasterDataEntity : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameMm { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}
