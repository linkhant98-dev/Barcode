using EMS.Infrastructure.Persistence;
using EMS.Web.Services;
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

    /// <summary>Exports the full matching audit trail (not just the 200-row on-screen preview) as CSV.</summary>
    public async Task<IActionResult> ExportCsv(string? user, string? module, CancellationToken ct)
    {
        var query = _db.AuditLogEntries.AsQueryable();
        if (!string.IsNullOrWhiteSpace(user)) query = query.Where(a => a.UserName.Contains(user));
        if (!string.IsNullOrWhiteSpace(module)) query = query.Where(a => a.Module.Contains(module));

        var entries = await query.OrderByDescending(a => a.TimestampUtc).ToListAsync(ct);
        var headers = new[] { "Timestamp", "User", "Action", "Module", "Reference", "Result" };
        var rows = entries.Select(a => (IReadOnlyList<object?>)new object?[]
        {
            a.TimestampUtc.ToString("dd MMM yyyy HH:mm:ss"), a.UserName, a.Action, a.Module, a.EntityReference, a.Result
        });

        var bytes = CsvExportHelper.Build(headers, rows);
        return File(bytes, "text/csv", $"Audit-Log-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }
}
