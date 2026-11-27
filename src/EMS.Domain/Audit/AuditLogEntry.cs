namespace EMS.Domain.Audit;

/// <summary>Section 15 - append-only audit trail. Never updated or deleted through the application (Immutability control).</summary>
public class AuditLogEntry
{
    public long AuditId { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // Login, Logout, Create, Edit, Submit, Decision, Export, Print, Download, Configure, AccessDenied
    public string Module { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityReference { get; set; }
    public string? BeforeValueJson { get; set; }
    public string? AfterValueJson { get; set; }
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
    public string Result { get; set; } = "Success";
}
