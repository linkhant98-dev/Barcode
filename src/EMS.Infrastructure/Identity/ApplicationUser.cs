using EMS.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace EMS.Infrastructure.Identity;

/// <summary>Section 12.1 - EMS user registration fields layered onto ASP.NET Core Identity.</summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public long? DepartmentId { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public bool MfaRequired { get; set; }
}

public class ApplicationRole : IdentityRole
{
    public string DescriptionEn { get; set; } = string.Empty;

    public ApplicationRole() { }

    public ApplicationRole(string roleName) : base(roleName) { }
}
