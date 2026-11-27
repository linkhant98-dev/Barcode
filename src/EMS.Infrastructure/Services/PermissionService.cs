using EMS.Application.Abstractions;
using EMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Services;

/// <summary>Section 12.2 - checks the current user's roles against RolePermission grants. System Administrator bypasses all checks.</summary>
public class PermissionService : IPermissionService
{
    private readonly EmsDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public PermissionService(EmsDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<bool> CurrentUserHasPermissionAsync(string permissionKey, CancellationToken ct = default)
    {
        if (_currentUser.IsInRole("System Administrator"))
            return true;

        var userRoles = _currentUser.Roles.ToList();
        if (userRoles.Count == 0)
            return false;

        return await _db.RolePermissions
            .AnyAsync(rp => rp.PermissionKey == permissionKey && userRoles.Contains(rp.RoleName), ct);
    }
}
