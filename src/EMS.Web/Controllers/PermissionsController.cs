using EMS.Application.Abstractions;
using EMS.Domain.Security;
using EMS.Infrastructure.Persistence;
using EMS.Infrastructure.Seed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Web.Controllers;

/// <summary>Section 12.2 - admin screen to grant/revoke module-level permissions per role.</summary>
[Authorize(Roles = "System Administrator")]
public class PermissionsController : Controller
{
    private readonly EmsDbContext _db;

    public PermissionsController(EmsDbContext db) => _db = db;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Permissions";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Administration", null), ("Permissions", null) };

        var grants = await _db.RolePermissions.ToListAsync(ct);
        ViewBag.Roles = DbSeeder.Roles.Where(r => r != "System Administrator").ToArray();
        ViewBag.PermissionKeys = Permissions.All;
        ViewBag.Grants = grants.Select(g => $"{g.RoleName}|{g.PermissionKey}").ToHashSet();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(List<string> grants, CancellationToken ct)
    {
        var existing = await _db.RolePermissions.ToListAsync(ct);
        _db.RolePermissions.RemoveRange(existing);

        foreach (var grant in grants.Distinct())
        {
            var parts = grant.Split('|', 2);
            if (parts.Length != 2) continue;
            if (!DbSeeder.Roles.Contains(parts[0]) || !Permissions.All.Contains(parts[1])) continue;

            _db.RolePermissions.Add(new RolePermission { RoleName = parts[0], PermissionKey = parts[1] });
        }

        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Permissions updated.";
        return RedirectToAction(nameof(Index));
    }
}
