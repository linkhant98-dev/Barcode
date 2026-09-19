namespace EMS.Domain.Security;

/// <summary>Section 12.2 - grants a permission key to every user in a given role. See Application.Abstractions.Permissions for the key vocabulary.</summary>
public class RolePermission
{
    public long RolePermissionId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string PermissionKey { get; set; } = string.Empty;
}
