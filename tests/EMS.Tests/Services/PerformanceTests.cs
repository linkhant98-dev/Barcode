using System.Diagnostics;
using EMS.Domain.Common;
using EMS.Domain.Reporting;
using EMS.Domain.Shareholders;
using EMS.Domain.Shares;
using EMS.Infrastructure.Services;
using EMS.Tests.TestSupport;
using Xunit;

namespace EMS.Tests.Services;

/// <summary>13.1.1 - DashboardService and ReportService deliberately aggregate the ledger client-side (a
/// documented SQLite SUM/GROUP-BY-over-decimal limitation), which only stays correct if it also stays roughly
/// linear in the number of ledger rows. These seed a moderate multi-shareholder, multi-group volume directly
/// (bypassing ShareLedgerService.PostAsync's per-call read-then-write, which would itself dominate the
/// timing) and assert a generous wall-clock ceiling - not a benchmark, just a tripwire against an accidental
/// O(n^2) (e.g. a per-row query introduced inside a loop).</summary>
public class PerformanceTests
{
    private const int ShareholderCount = 3000;

    private static void SeedLedgerAtScale(SqliteTestDb db)
    {
        var groups = new[]
        {
            TestSeed.Group(db.Context, "PERSONAL"),
            TestSeed.Group(db.Context, "STAFF"),
            TestSeed.Group(db.Context, "PUBLIC_COMPANY")
        };
        var shareClass = TestSeed.ShareClass(db.Context);

        // Bulk-constructed and saved once, rather than looping TestSeed.ActiveShareholder (one SaveChanges per
        // call) - at this row count that per-call round trip, not the code under test, would dominate the timing.
        var shareholders = new List<Shareholder>(ShareholderCount);
        for (var i = 0; i < ShareholderCount; i++)
        {
            shareholders.Add(new Shareholder
            {
                ShareholderNo = $"SH-90{i:D6}",
                Type = ApplicantType.Personal,
                ShareholderGroupId = groups[i % groups.Length].Id,
                Status = ShareholderStatus.Active,
                KycStatus = KycResult.Approved,
                RegistrationDate = new DateOnly(2024, 1, 1),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "system",
                Person = new Person { NameEn = $"Perf Test {i}", NameMm = $"Perf Test {i}", DateOfBirth = new DateOnly(1985, 1, 1), FatherName = "-", NrcNumber = $"12/YAKANA(N){i:D6}" }
            });
        }
        db.Context.Shareholders.AddRange(shareholders);
        db.Context.SaveChanges();

        var ledgerEntries = new List<ShareLedgerEntry>(ShareholderCount);
        for (var i = 0; i < ShareholderCount; i++)
        {
            var qty = 1000m + i;
            ledgerEntries.Add(new ShareLedgerEntry
            {
                ShareholderId = shareholders[i].Id,
                ShareClassId = shareClass.Id,
                SourceReference = "PERF-SEED",
                QuantityDelta = qty,
                CapitalAmountDelta = qty * 10_000m,
                RunningQuantityBalance = qty,
                RunningPaidUpCapital = qty * 10_000m,
                EffectiveDate = new DateOnly(2026, 1, 1),
                PostedAtUtc = DateTime.UtcNow,
                PostedByUserId = "system"
            });
        }
        db.Context.ShareLedgerEntries.AddRange(ledgerEntries);
        db.Context.SaveChanges();
    }

    [Fact]
    public async Task GetDashboardAsync_AtModerateScale_CompletesWellUnderAGenerousCeiling()
    {
        using var db = new SqliteTestDb();
        SeedLedgerAtScale(db);
        var service = new DashboardService(db.Context);

        var sw = Stopwatch.StartNew();
        var view = await service.GetDashboardAsync(currentUserId: null, asOfDate: null);
        sw.Stop();

        Assert.Equal(ShareholderCount, view.Kpis.RegisteredShareholders);
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"GetDashboardAsync took {sw.ElapsedMilliseconds}ms for {ShareholderCount} shareholders - expected well under 5000ms.");
    }

    [Fact]
    public async Task RunAsync_ShareholdersList_AtModerateScale_CompletesWellUnderAGenerousCeiling()
    {
        using var db = new SqliteTestDb();
        SeedLedgerAtScale(db);
        db.Context.ReportDefinitions.Add(new ReportDefinition { ReportCode = "RPT-001", NameEn = "Shareholders List", NameMm = "Shareholders List", Category = "Test" });
        db.Context.SaveChanges();
        var user = new FakeCurrentUserService();
        var service = new ReportService(db.Context, user, new PermissionService(db.Context, user));

        var sw = Stopwatch.StartNew();
        var result = await service.RunAsync("RPT-001", new EMS.Application.Reporting.ReportParameters());
        sw.Stop();

        Assert.Equal(ShareholderCount, result.Rows.Count);
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"RPT-001 took {sw.ElapsedMilliseconds}ms for {ShareholderCount} shareholders - expected well under 5000ms.");
    }

    [Fact]
    public async Task RunAsync_ShareholderGroupSummary_AtModerateScale_CompletesWellUnderAGenerousCeiling()
    {
        using var db = new SqliteTestDb();
        SeedLedgerAtScale(db);
        db.Context.ReportDefinitions.Add(new ReportDefinition { ReportCode = "RPT-002", NameEn = "Shareholders Group Summary", NameMm = "Shareholders Group Summary", Category = "Test" });
        db.Context.SaveChanges();
        var user = new FakeCurrentUserService();
        var service = new ReportService(db.Context, user, new PermissionService(db.Context, user));

        var sw = Stopwatch.StartNew();
        var result = await service.RunAsync("RPT-002", new EMS.Application.Reporting.ReportParameters());
        sw.Stop();

        Assert.Equal(3, result.Rows.Count); // three groups
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"RPT-002 took {sw.ElapsedMilliseconds}ms for {ShareholderCount} shareholders - expected well under 5000ms.");
    }
}
