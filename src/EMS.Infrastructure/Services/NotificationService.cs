using EMS.Application.Abstractions;
using EMS.Domain.Common;
using EMS.Domain.Notifications;
using EMS.Infrastructure.Identity;
using EMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace EMS.Infrastructure.Services;

/// <summary>
/// Section 14 - notification center. Only the InApp channel is actually delivered (a row the user sees in
/// the header bell / notification list); Email/SMS templates would need a real gateway wired to Notify*Async
/// before they do anything beyond what's modeled here.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly EmsDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationService(EmsDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task NotifyUserAsync(string userId, string templateCode, string title, string body, string? entityReference = null, string? linkUrl = null, CancellationToken ct = default)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Channel = NotificationChannel.InApp,
            TemplateCode = templateCode,
            Title = title,
            Body = body,
            EntityReference = entityReference,
            LinkUrl = linkUrl,
            Status = NotificationStatus.Sent,
            CreatedAtUtc = DateTime.UtcNow,
            SentAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task NotifyRoleAsync(string role, string templateCode, string title, string body, string? entityReference = null, string? linkUrl = null, CancellationToken ct = default)
    {
        var users = await _userManager.GetUsersInRoleAsync(role);
        foreach (var user in users)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Channel = NotificationChannel.InApp,
                TemplateCode = templateCode,
                Title = title,
                Body = body,
                EntityReference = entityReference,
                LinkUrl = linkUrl,
                Status = NotificationStatus.Sent,
                CreatedAtUtc = DateTime.UtcNow,
                SentAtUtc = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
