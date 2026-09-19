using EMS.Domain.Common;
using EMS.Infrastructure.Services;
using EMS.Tests.TestSupport;
using Xunit;

namespace EMS.Tests.Services;

/// <summary>17 - notification fan-out to a single user vs. every user currently holding a role.</summary>
public class NotificationServiceTests
{
    [Fact]
    public async Task NotifyUserAsync_InsertsASentInAppNotificationForThatUser()
    {
        using var host = new IdentityTestHost();
        var user = await host.CreateUserAsync("maker@ems.local");
        var service = new NotificationService(host.Context, host.Users);

        await service.NotifyUserAsync(user.Id, "SA_SUBMITTED", "Title", "Body text", "SA-2026-000001", "/ShareholderApplications/Edit/1");

        var notification = Assert.Single(host.Context.Notifications);
        Assert.Equal(user.Id, notification.UserId);
        Assert.Equal(NotificationChannel.InApp, notification.Channel);
        Assert.Equal("SA_SUBMITTED", notification.TemplateCode);
        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.Equal("SA-2026-000001", notification.EntityReference);
        Assert.NotNull(notification.SentAtUtc);
    }

    [Fact]
    public async Task NotifyRoleAsync_InsertsOneNotificationPerUserCurrentlyInThatRole()
    {
        using var host = new IdentityTestHost();
        await host.Roles.CreateAsync(new EMS.Infrastructure.Identity.ApplicationRole("KYC Officer"));
        var officer1 = await host.CreateUserAsync("officer1@ems.local");
        var officer2 = await host.CreateUserAsync("officer2@ems.local");
        var maker = await host.CreateUserAsync("maker@ems.local"); // not in the role
        await host.Users.AddToRoleAsync(officer1, "KYC Officer");
        await host.Users.AddToRoleAsync(officer2, "KYC Officer");
        var service = new NotificationService(host.Context, host.Users);

        await service.NotifyRoleAsync("KYC Officer", "SA_SUBMITTED", "New application", "Body text");

        var notifications = host.Context.Notifications.ToList();
        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, n => n.UserId == officer1.Id);
        Assert.Contains(notifications, n => n.UserId == officer2.Id);
        Assert.DoesNotContain(notifications, n => n.UserId == maker.Id);
    }

    [Fact]
    public async Task NotifyRoleAsync_NoUsersInTheRole_InsertsNothing()
    {
        using var host = new IdentityTestHost();
        await host.Roles.CreateAsync(new EMS.Infrastructure.Identity.ApplicationRole("Board Approver"));
        var service = new NotificationService(host.Context, host.Users);

        await service.NotifyRoleAsync("Board Approver", "TS_SUBMITTED", "Title", "Body");

        Assert.Empty(host.Context.Notifications);
    }
}
