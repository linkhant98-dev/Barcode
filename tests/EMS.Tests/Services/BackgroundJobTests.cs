using EMS.Application.Abstractions;
using EMS.Domain.Common;
using EMS.Domain.Workflow;
using EMS.Infrastructure.Jobs;
using EMS.Infrastructure.Persistence;
using EMS.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EMS.Tests.Services;

/// <summary>21.1 - drives ApprovalReminderJob/ReconciliationJob's single-pass logic directly (RunOnceAsync is
/// internal for exactly this reason) rather than waiting on their real PeriodicTimer intervals.</summary>
public class BackgroundJobTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public BackgroundJobTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<EmsDbContext>(options => options.UseSqlite(_connection));
        services.AddSingleton<INotificationService, FakeNotificationService>();
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<EmsDbContext>().Database.EnsureCreated();
    }

    private EmsDbContext NewContext() => _provider.GetRequiredService<EmsDbContext>();

    [Fact]
    public async Task ApprovalReminderJob_OverduePendingStep_NotifiesTheApproverRoleAndStampsLastReminder()
    {
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmsDbContext>();
            var instance = new ApprovalInstance
            {
                EntityType = "ShareTransaction",
                EntityId = 1,
                EntityReference = "IS-2026-000001",
                Status = WorkflowStatus.PendingApproval,
                SubmittedByUserId = "maker",
                SubmittedAtUtc = DateTime.UtcNow.AddHours(-30),
                CurrentSequence = 1,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "system"
            };
            instance.Steps.Add(new ApprovalStep { Sequence = 1, StepName = "DGM", ApproverRole = "DGM Approver", Status = ApprovalStepStatus.Pending, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "system" });
            db.ApprovalInstances.Add(instance);
            db.SaveChanges();
        }

        var job = new ApprovalReminderJob(_provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ApprovalReminderJob>.Instance);
        await job.RunOnceAsync(CancellationToken.None);

        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EmsDbContext>();
        var step = verifyDb.ApprovalSteps.Single();
        Assert.NotNull(step.LastReminderAtUtc);
        var fakeNotifications = (FakeNotificationService)_provider.GetRequiredService<INotificationService>();
        Assert.Contains(fakeNotifications.RoleNotifications, n => n.Role == "DGM Approver" && n.TemplateCode == "APPROVAL_OVERDUE");
    }

    [Fact]
    public async Task ApprovalReminderJob_StepNotYetOverdue_IsNotNotified()
    {
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmsDbContext>();
            var instance = new ApprovalInstance
            {
                EntityType = "ShareTransaction",
                EntityId = 1,
                EntityReference = "IS-2026-000002",
                Status = WorkflowStatus.PendingApproval,
                SubmittedByUserId = "maker",
                SubmittedAtUtc = DateTime.UtcNow.AddHours(-1),
                CurrentSequence = 1,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "system"
            };
            instance.Steps.Add(new ApprovalStep { Sequence = 1, StepName = "DGM", ApproverRole = "DGM Approver", Status = ApprovalStepStatus.Pending, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "system" });
            db.ApprovalInstances.Add(instance);
            db.SaveChanges();
        }

        var job = new ApprovalReminderJob(_provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ApprovalReminderJob>.Instance);
        await job.RunOnceAsync(CancellationToken.None);

        var fakeNotifications = (FakeNotificationService)_provider.GetRequiredService<INotificationService>();
        Assert.Empty(fakeNotifications.RoleNotifications);
    }

    [Fact]
    public async Task ApprovalReminderJob_AlreadyRemindedRecently_IsNotRemindedAgain()
    {
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmsDbContext>();
            var instance = new ApprovalInstance
            {
                EntityType = "ShareTransaction",
                EntityId = 1,
                EntityReference = "IS-2026-000003",
                Status = WorkflowStatus.PendingApproval,
                SubmittedByUserId = "maker",
                SubmittedAtUtc = DateTime.UtcNow.AddHours(-48),
                CurrentSequence = 1,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "system"
            };
            instance.Steps.Add(new ApprovalStep { Sequence = 1, StepName = "DGM", ApproverRole = "DGM Approver", Status = ApprovalStepStatus.Pending, LastReminderAtUtc = DateTime.UtcNow.AddHours(-1), CreatedAtUtc = DateTime.UtcNow, CreatedBy = "system" });
            db.ApprovalInstances.Add(instance);
            db.SaveChanges();
        }

        var job = new ApprovalReminderJob(_provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ApprovalReminderJob>.Instance);
        await job.RunOnceAsync(CancellationToken.None);

        var fakeNotifications = (FakeNotificationService)_provider.GetRequiredService<INotificationService>();
        Assert.Empty(fakeNotifications.RoleNotifications);
    }

    [Fact]
    public async Task ReconciliationJob_NegativeRunningBalance_CreatesAnOpenExceptionOnce()
    {
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmsDbContext>();
            var group = TestSeed.Group(db);
            var shareClass = TestSeed.ShareClass(db);
            var shareholder = TestSeed.ActiveShareholder(db, group.Id, "SH-24000001");
            db.ShareLedgerEntries.Add(new EMS.Domain.Shares.ShareLedgerEntry
            {
                ShareholderId = shareholder.Id,
                ShareClassId = shareClass.Id,
                QuantityDelta = -500m,
                CapitalAmountDelta = -5_000_000m,
                RunningQuantityBalance = -500m,
                RunningPaidUpCapital = -5_000_000m,
                SourceReference = "TS-TEST",
                EffectiveDate = new DateOnly(2026, 6, 1),
                PostedAtUtc = DateTime.UtcNow,
                PostedByUserId = "system"
            });
            db.SaveChanges();
        }

        var job = new ReconciliationJob(_provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ReconciliationJob>.Instance);
        await job.RunOnceAsync(CancellationToken.None);
        await job.RunOnceAsync(CancellationToken.None); // second run on the same business date must not duplicate

        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EmsDbContext>();
        var exception = Assert.Single(verifyDb.ReconciliationResults);
        Assert.Equal("Open", exception.Status);
        Assert.Equal(-500m, exception.ActualValue);
    }

    /// <summary>CheckDuplicateCertificatesAsync's duplicate-number branch cannot actually be exercised
    /// end-to-end: IX_ShareCertificates_CertificateNumber is a unique index, so EF throws on the second insert
    /// before the job ever runs. That means the branch is effectively dead code under the current schema,
    /// reachable only if a duplicate were ever written outside EF (raw SQL, a migration bug). This test covers
    /// the reachable path instead: unique certificate numbers never produce a false-positive exception.</summary>
    [Fact]
    public async Task ReconciliationJob_AllCertificateNumbersUnique_RecordsNoException()
    {
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmsDbContext>();
            var group = TestSeed.Group(db);
            var shareClass = TestSeed.ShareClass(db);
            var a = TestSeed.ActiveShareholder(db, group.Id, "SH-24000001", "Alice");
            var b = TestSeed.ActiveShareholder(db, group.Id, "SH-24000002", "Bob");
            db.ShareCertificates.Add(new EMS.Domain.Shares.ShareCertificate { CertificateNumber = "CERT-0001", ShareholderId = a.Id, ShareClassId = shareClass.Id, Quantity = 100m, Status = CertificateStatus.Printed, IssueDate = new DateOnly(2026, 1, 1), CreatedAtUtc = DateTime.UtcNow, CreatedBy = "system" });
            db.ShareCertificates.Add(new EMS.Domain.Shares.ShareCertificate { CertificateNumber = "CERT-0002", ShareholderId = b.Id, ShareClassId = shareClass.Id, Quantity = 50m, Status = CertificateStatus.Printed, IssueDate = new DateOnly(2026, 1, 2), CreatedAtUtc = DateTime.UtcNow, CreatedBy = "system" });
            db.SaveChanges();
        }

        var job = new ReconciliationJob(_provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ReconciliationJob>.Instance);
        await job.RunOnceAsync(CancellationToken.None);

        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EmsDbContext>();
        Assert.Empty(verifyDb.ReconciliationResults);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }
}
