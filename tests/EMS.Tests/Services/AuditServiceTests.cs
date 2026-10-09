using EMS.Infrastructure.Services;
using EMS.Tests.TestSupport;
using Xunit;

namespace EMS.Tests.Services;

public class AuditServiceTests
{
    [Fact]
    public async Task LogAsync_PersistsAllSuppliedFieldsUnderTheCurrentUser()
    {
        using var db = new SqliteTestDb();
        var user = new FakeCurrentUserService { UserId = "u1", UserName = "maker@ems.local", IpAddress = "10.0.0.5" };
        var service = new AuditService(db.Context, user);

        await service.LogAsync("Submit", "IS", "ShareTransaction", "IS-2026-000001", after: new { Amount = 100 });

        var entry = Assert.Single(db.Context.AuditLogEntries);
        Assert.Equal("Submit", entry.Action);
        Assert.Equal("IS", entry.Module);
        Assert.Equal("ShareTransaction", entry.EntityType);
        Assert.Equal("IS-2026-000001", entry.EntityReference);
        Assert.Equal("u1", entry.UserId);
        Assert.Equal("maker@ems.local", entry.UserName);
        Assert.Equal("10.0.0.5", entry.IpAddress);
        Assert.Equal("Success", entry.Result);
        Assert.Contains("100", entry.AfterValueJson);
        Assert.Null(entry.BeforeValueJson);
        Assert.False(string.IsNullOrEmpty(entry.CorrelationId));
    }

    [Fact]
    public async Task LogAsync_WithoutBeforeOrAfter_LeavesBothJsonColumnsNull()
    {
        using var db = new SqliteTestDb();
        var service = new AuditService(db.Context, new FakeCurrentUserService());

        await service.LogAsync("Decision", "TS", "ShareTransaction", "TS-2026-000001");

        var entry = Assert.Single(db.Context.AuditLogEntries);
        Assert.Null(entry.BeforeValueJson);
        Assert.Null(entry.AfterValueJson);
    }

    [Fact]
    public async Task LogAsync_MultipleCalls_EachGetsAUniqueCorrelationId()
    {
        using var db = new SqliteTestDb();
        var service = new AuditService(db.Context, new FakeCurrentUserService());

        await service.LogAsync("Create", "SA", "ShareholderApplication", "SA-2026-000001");
        await service.LogAsync("Edit", "SA", "ShareholderApplication", "SA-2026-000001");

        var ids = db.Context.AuditLogEntries.Select(a => a.CorrelationId).ToList();
        Assert.Equal(2, ids.Distinct().Count());
    }
}
