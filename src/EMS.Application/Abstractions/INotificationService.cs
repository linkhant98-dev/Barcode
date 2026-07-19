namespace EMS.Application.Abstractions;

/// <summary>Section 14 - notification center. In-app notifications are created directly; email/SMS are logged as queued only (no real gateway wired up).</summary>
public interface INotificationService
{
    Task NotifyUserAsync(string userId, string templateCode, string title, string body, string? entityReference = null, string? linkUrl = null, CancellationToken ct = default);

    /// <summary>Creates one notification per active user currently in the given role.</summary>
    Task NotifyRoleAsync(string role, string templateCode, string title, string body, string? entityReference = null, string? linkUrl = null, CancellationToken ct = default);
}
