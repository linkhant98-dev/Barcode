using EMS.Domain.Security;
using EMS.Infrastructure.Services;
using EMS.Tests.TestSupport;
using Xunit;

namespace EMS.Tests.Services;

public class PermissionServiceTests
{
    [Fact]
    public async Task CurrentUserHasPermissionAsync_SystemAdministrator_BypassesEveryCheck()
    {
        using var db = new SqliteTestDb();
        var user = new FakeCurrentUserService { RoleList = ["System Administrator"] };
        var service = new PermissionService(db.Context, user);

        var result = await service.CurrentUserHasPermissionAsync("Some.Permission.NoOneGranted");

        Assert.True(result);
    }

    [Fact]
    public async Task CurrentUserHasPermissionAsync_RoleWithNoGrants_ReturnsFalse()
    {
        using var db = new SqliteTestDb();
        var user = new FakeCurrentUserService { RoleList = ["Report Viewer"] };
        var service = new PermissionService(db.Context, user);

        var result = await service.CurrentUserHasPermissionAsync("Reports.Export");

        Assert.False(result);
    }

    [Fact]
    public async Task CurrentUserHasPermissionAsync_RoleWithMatchingGrant_ReturnsTrue()
    {
        using var db = new SqliteTestDb();
        db.Context.RolePermissions.Add(new RolePermission { RoleName = "Report Viewer", PermissionKey = "Reports.Export" });
        db.Context.SaveChanges();

        var user = new FakeCurrentUserService { RoleList = ["Report Viewer"] };
        var service = new PermissionService(db.Context, user);

        var result = await service.CurrentUserHasPermissionAsync("Reports.Export");

        Assert.True(result);
    }

    [Fact]
    public async Task CurrentUserHasPermissionAsync_GrantExistsForADifferentRole_ReturnsFalse()
    {
        using var db = new SqliteTestDb();
        db.Context.RolePermissions.Add(new RolePermission { RoleName = "Auditor", PermissionKey = "Reports.ViewSensitiveData" });
        db.Context.SaveChanges();

        var user = new FakeCurrentUserService { RoleList = ["Report Viewer"] };
        var service = new PermissionService(db.Context, user);

        var result = await service.CurrentUserHasPermissionAsync("Reports.ViewSensitiveData");

        Assert.False(result);
    }

    [Fact]
    public async Task CurrentUserHasPermissionAsync_NoRolesAtAll_ReturnsFalseWithoutQuerying()
    {
        using var db = new SqliteTestDb();
        var user = new FakeCurrentUserService { RoleList = [] };
        var service = new PermissionService(db.Context, user);

        var result = await service.CurrentUserHasPermissionAsync("Reports.View");

        Assert.False(result);
    }
}
