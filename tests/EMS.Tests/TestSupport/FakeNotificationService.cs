using EMS.Application.Abstractions;

namespace EMS.Tests.TestSupport;

/// <summary>Records calls instead of touching Identity's UserManager/role store, so business-logic tests
/// (workflow routing, ledger posting, ...) don't need a full Identity setup just to satisfy this dependency.
/// NotificationService itself is exercised directly, with a real UserManager, in NotificationServiceTests.</summary>
public sealed class FakeNotificationService : INotificationService
{
    public record UserNotification(string UserId, string TemplateCode, string Title, string Body, string? EntityReference, string? LinkUrl);
    public record RoleNotification(string Role, string TemplateCode, string Title, string Body, string? EntityReference, string? LinkUrl);

    public List<UserNotification> UserNotifications { get; } = [];
    public List<RoleNotification> RoleNotifications { get; } = [];

    public Task NotifyUserAsync(string userId, string templateCode, string title, string body, string? entityReference = null, string? linkUrl = null, CancellationToken ct = default)
    {
        UserNotifications.Add(new UserNotification(userId, templateCode, title, body, entityReference, linkUrl));
        return Task.CompletedTask;
    }

    public Task NotifyRoleAsync(string role, string templateCode, string title, string body, string? entityReference = null, string? linkUrl = null, CancellationToken ct = default)
    {
        RoleNotifications.Add(new RoleNotification(role, templateCode, title, body, entityReference, linkUrl));
        return Task.CompletedTask;
    }
}
