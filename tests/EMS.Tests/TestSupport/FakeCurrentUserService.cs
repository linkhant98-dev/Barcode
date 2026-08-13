using EMS.Application.Abstractions;

namespace EMS.Tests.TestSupport;

/// <summary>A hand-rolled test double instead of a mocking library - the interface is tiny and every
/// service under test only reads these four members.</summary>
public sealed class FakeCurrentUserService : ICurrentUserService
{
    public string UserId { get; set; } = "test-user";
    public string UserName { get; set; } = "test-user@ems.local";
    public List<string> RoleList { get; set; } = [];
    public string? IpAddress { get; set; } = "127.0.0.1";

    public IReadOnlyCollection<string> Roles => RoleList;

    public bool IsInRole(string role) => RoleList.Contains(role);
}
