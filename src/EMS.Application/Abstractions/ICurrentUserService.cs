namespace EMS.Application.Abstractions;

/// <summary>Abstraction over the authenticated principal so Application/Infrastructure services never touch HttpContext directly.</summary>
public interface ICurrentUserService
{
    string UserId { get; }
    string UserName { get; }
    IReadOnlyCollection<string> Roles { get; }
    string? IpAddress { get; }
    bool IsInRole(string role);
}
