using EMS.Domain.Common;

namespace EMS.Domain.Notifications;

/// <summary>Section 14 / 16.1 - notification center entries and dispatch tracking.</summary>
public class Notification
{
    public long NotificationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? EntityReference { get; set; }
    public string? LinkUrl { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Queued;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public bool IsRead { get; set; }
}
