using System.Security.Claims;
using EMS.Application.Abstractions;

namespace EMS.Web.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";

    public string UserName => User?.Identity?.Name ?? "system";

    public IReadOnlyCollection<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
