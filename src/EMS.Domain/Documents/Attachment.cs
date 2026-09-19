using EMS.Domain.Common;

namespace EMS.Domain.Documents;

/// <summary>16.1 / 14.1 - attachment metadata; file bytes live in the encrypted file repository, not in SQL (COM-005).</summary>
public class Attachment : AuditableEntity
{
    public AttachmentOwnerType OwnerType { get; set; }
    public long OwnerId { get; set; }
    public long? DocumentTypeId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string Sha256Checksum { get; set; } = string.Empty;
    public int Version { get; set; } = 1;

    public string UploadedByUserId { get; set; } = string.Empty;
    public DateTime UploadedAtUtc { get; set; }
    public bool IsSoftDeleted { get; set; }
}
