namespace EMS.Application.Abstractions;

/// <summary>
/// Section 12.2 - action-level permission checks (module x action), layered on top of the coarser
/// role-based [Authorize(Roles=...)] gates already used for admin screens. System Administrator always
/// passes every check (superuser), regardless of RolePermission grants.
/// </summary>
public interface IPermissionService
{
    Task<bool> CurrentUserHasPermissionAsync(string permissionKey, CancellationToken ct = default);
}
