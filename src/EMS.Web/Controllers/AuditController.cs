using EMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Web.Controllers;

/// <summary>Section 15.4 - read-only audit log viewer.</summary>
[Authorize(Roles = "System Administrator,Auditor")]
public class AuditController : Controller
{
    private readonly EmsDbContext _db;

    public AuditController(EmsDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? user, string? module, CancellationToken ct)
    {
        ViewData["Title"] = "Audit Log";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Administration", null), ("Audit Log", null) };

        var query = _db.AuditLogEntries.AsQueryable();
        if (!string.IsNullOrWhiteSpace(user)) query = query.Where(a => a.UserName.Contains(user));
        if (!string.IsNullOrWhiteSpace(module)) query = query.Where(a => a.Module.Contains(module));

        ViewBag.UserFilter = user;
        ViewBag.ModuleFilter = module;

        var entries = await query.OrderByDescending(a => a.TimestampUtc).Take(200).ToListAsync(ct);
        return View(entries);
    }
}
