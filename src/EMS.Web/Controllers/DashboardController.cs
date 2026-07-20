using EMS.Application.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.Web.Controllers;

/// <summary>Section 13.2 - the dashboard is the first page after successful authentication.</summary>
[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService) => _dashboardService = dashboardService;

    public async Task<IActionResult> Index(DateOnly? asOf, CancellationToken ct)
    {
        var data = await _dashboardService.GetDashboardAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, asOf, ct);
        return View(data);
    }
}
