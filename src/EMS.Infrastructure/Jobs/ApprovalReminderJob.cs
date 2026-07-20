using EMS.Application.Abstractions;
using EMS.Domain.Workflow;
using EMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EMS.Infrastructure.Jobs;

/// <summary>
/// Section 21.1 "Approval reminders" (spec target: hourly). Runs on a shorter interval here so the
/// behavior is observable without waiting an hour; each pending step is only nudged again after
/// <see cref="ReminderInterval"/> has passed since its last reminder, so it does not spam every tick.
/// </summary>
public class ApprovalReminderJob : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan OverdueAfter = TimeSpan.FromHours(24);
    private static readonly TimeSpan ReminderInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ApprovalReminderJob> _logger;

    public ApprovalReminderJob(IServiceScopeFactory scopeFactory, ILogger<ApprovalReminderJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Approval reminder job run failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmsDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;

        var overdueSteps = await db.ApprovalSteps
            .Include(s => s.ApprovalInstance)
            .Where(s => s.Status == Domain.Common.ApprovalStepStatus.Pending
                && s.ApprovalInstance!.SubmittedAtUtc <= now - OverdueAfter
                && (s.LastReminderAtUtc == null || s.LastReminderAtUtc <= now - ReminderInterval))
            .ToListAsync(ct);

        foreach (var step in overdueSteps)
        {
            var instance = step.ApprovalInstance!;
            await notifications.NotifyRoleAsync(step.ApproverRole, "APPROVAL_OVERDUE",
                $"Approval overdue - {instance.EntityReference}",
                $"{instance.EntityReference} has been awaiting your {step.StepName} decision since {instance.SubmittedAtUtc:dd MMM yyyy}.",
                instance.EntityReference, $"/Approvals/Review/{step.Id}", ct);

            step.LastReminderAtUtc = now;
        }

        if (overdueSteps.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Approval reminder job sent {Count} overdue notifications.", overdueSteps.Count);
        }
    }
}
