using EMS.Application.Workflow;
using EMS.Infrastructure.Services;
using EMS.Tests.TestSupport;
using Xunit;

namespace EMS.Tests.Services;

public class DelegationServiceTests
{
    private static DelegationService BuildService(SqliteTestDb db, FakeCurrentUserService user) =>
        new(db.Context, user, new AuditService(db.Context, user));

    [Fact]
    public async Task CreateAsync_ToSelf_Throws()
    {
        using var db = new SqliteTestDb();
        var user = new FakeCurrentUserService { UserId = "u1" };
        var service = BuildService(db, user);

        var request = new CreateDelegationRequest("u1", "DGM Approver", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "Leave");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_EndDateBeforeStartDate_Throws()
    {
        using var db = new SqliteTestDb();
        var user = new FakeCurrentUserService { UserId = "u1" };
        var service = BuildService(db, user);

        var request = new CreateDelegationRequest("u2", "DGM Approver", new DateOnly(2026, 1, 31), new DateOnly(2026, 1, 1), "Leave");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsAnActiveDelegation()
    {
        using var db = new SqliteTestDb();
        var user = new FakeCurrentUserService { UserId = "u1" };
        var service = BuildService(db, user);

        var request = new CreateDelegationRequest("u2", "DGM Approver", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "Annual leave");
        var id = await service.CreateAsync(request);

        var delegation = db.Context.Delegations.Single(d => d.Id == id);
        Assert.Equal("u1", delegation.FromUserId);
        Assert.Equal("u2", delegation.ToUserId);
        Assert.True(delegation.IsActive);
    }

    [Fact]
    public async Task EndAsync_DeactivatesTheDelegation()
    {
        using var db = new SqliteTestDb();
        var user = new FakeCurrentUserService { UserId = "u1" };
        var service = BuildService(db, user);

        var id = await service.CreateAsync(new CreateDelegationRequest("u2", "DGM Approver", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "Leave"));
        await service.EndAsync(id);

        var delegation = db.Context.Delegations.Single(d => d.Id == id);
        Assert.False(delegation.IsActive);
    }

    [Fact]
    public async Task EndAsync_UnknownId_Throws()
    {
        using var db = new SqliteTestDb();
        var service = BuildService(db, new FakeCurrentUserService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EndAsync(999));
    }
}
