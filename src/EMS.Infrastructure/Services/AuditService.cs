using System.Text.Json;
using EMS.Application.Abstractions;
using EMS.Domain.Audit;
using EMS.Infrastructure.Persistence;

namespace EMS.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly EmsDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AuditService(EmsDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task LogAsync(
        string action,
        string module,
        string entityType,
        string? entityReference = null,
        object? before = null,
        object? after = null,
        string result = "Success",
        CancellationToken ct = default)
    {
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            TimestampUtc = DateTime.UtcNow,
            UserId = _currentUser.UserId,
            UserName = _currentUser.UserName,
            Action = action,
            Module = module,
            EntityType = entityType,
            EntityReference = entityReference,
            BeforeValueJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterValueJson = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = _currentUser.IpAddress,
            CorrelationId = Guid.NewGuid().ToString("N"),
            Result = result
        });

        await _db.SaveChangesAsync(ct);
    }
}
