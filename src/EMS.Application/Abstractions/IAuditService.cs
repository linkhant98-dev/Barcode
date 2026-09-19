namespace EMS.Application.Abstractions;

/// <summary>Section 15 - append-only audit trail writer.</summary>
public interface IAuditService
{
    Task LogAsync(
        string action,
        string module,
        string entityType,
        string? entityReference = null,
        object? before = null,
        object? after = null,
        string result = "Success",
        CancellationToken ct = default);
}
