using EMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Web.Controllers;

/// <summary>Section 16.1 - notification center: full list and read-state management.</summary>
[Authorize]
public class NotificationsController : Controller
{
    private readonly EmsDbContext _db;

    public NotificationsController(EmsDbContext db) => _db = db;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Notifications";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Notifications", null) };

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(100)
            .ToListAsync(ct);

        return View(notifications);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(long id, string? returnUrl, CancellationToken ct)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId, ct);
        if (notification is not null)
        {
            notification.IsRead = true;
            await _db.SaveChangesAsync(ct);
        }

        return string.IsNullOrEmpty(returnUrl) ? RedirectToAction(nameof(Index)) : LocalRedirect(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        var unread = await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync(ct);
        foreach (var n in unread) n.IsRead = true;
        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Index));
    }
}
