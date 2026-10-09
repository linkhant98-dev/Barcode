using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using EMS.Web.Models;

namespace EMS.Web.Controllers;

/// <summary>
/// Section 19 - technical error details are never exposed to end users; only a reference/correlation ID is shown.
/// </summary>
public class HomeController : Controller
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
