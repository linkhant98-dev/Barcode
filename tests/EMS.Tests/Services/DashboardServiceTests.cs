using EMS.Application.Shares;
using EMS.Infrastructure.Services;
using EMS.Tests.TestSupport;
using Xunit;

namespace EMS.Tests.Services;

/// <summary>13.1.1 - dashboard KPIs and widgets are derived directly from the ledger, never a maintained total.</summary>
public class DashboardServiceTests
{
    [Fact]
    public async Task GetDashboardAsync_KpisReflectPostedLedgerEntriesAndActiveShareholders()
    {
        using var db = new SqliteTestDb();
        var group = TestSeed.Group(db.Context);
        var shareClass = TestSeed.ShareClass(db.Context);
        var active = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001");
        var suspended = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000002", status: EMS.Domain.Common.ShareholderStatus.Suspended);
        var ledger = new ShareLedgerService(db.Context, new FakeCurrentUserService());
        await ledger.PostAsync(new PostLedgerEntryRequest(active.Id, shareClass.Id, 1000m, 10_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        var service = new DashboardService(db.Context);

        var view = await service.GetDashboardAsync(currentUserId: null, asOfDate: null);

        Assert.Equal(1, view.Kpis.RegisteredShareholders); // only the active shareholder counts
        Assert.Equal(1000m, view.Kpis.TotalShares);
        Assert.Equal(10_000_000m, view.Kpis.PaidUpCapital);
        Assert.Equal(0, view.Kpis.PendingApprovals);
    }

    [Fact]
    public async Task GetDashboardAsync_LedgerEntriesAfterTheAsOfDate_AreExcludedFromKpisAndGroupBreakdown()
    {
        using var db = new SqliteTestDb();
        var group = TestSeed.Group(db.Context);
        var shareClass = TestSeed.ShareClass(db.Context);
        var shareholder = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001");
        var ledger = new ShareLedgerService(db.Context, new FakeCurrentUserService());
        await ledger.PostAsync(new PostLedgerEntryRequest(shareholder.Id, shareClass.Id, 1000m, 10_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        await ledger.PostAsync(new PostLedgerEntryRequest(shareholder.Id, shareClass.Id, 500m, 5_000_000m, 0m, "TOP-UP", null, null, null, new DateOnly(2026, 6, 1)));
        var service = new DashboardService(db.Context);

        var view = await service.GetDashboardAsync(currentUserId: null, asOfDate: new DateOnly(2026, 3, 1));

        Assert.Equal(1000m, view.Kpis.TotalShares);
        var groupItem = Assert.Single(view.ShareholdingByGroup);
        Assert.Equal(1000m, groupItem.Shares);
        Assert.Equal(100m, groupItem.Percentage);
    }

    [Fact]
    public async Task GetDashboardAsync_ShareholdingByGroup_ComputesPercentageAndOrdersByShareCountDescending()
    {
        using var db = new SqliteTestDb();
        var groupA = TestSeed.Group(db.Context, "PERSONAL");
        var groupB = TestSeed.Group(db.Context, "STAFF");
        var shareClass = TestSeed.ShareClass(db.Context);
        var a = TestSeed.ActiveShareholder(db.Context, groupA.Id, "SH-24000001");
        var b = TestSeed.ActiveShareholder(db.Context, groupB.Id, "SH-24000002");
        var ledger = new ShareLedgerService(db.Context, new FakeCurrentUserService());
        await ledger.PostAsync(new PostLedgerEntryRequest(a.Id, shareClass.Id, 3000m, 30_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        await ledger.PostAsync(new PostLedgerEntryRequest(b.Id, shareClass.Id, 1000m, 10_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        var service = new DashboardService(db.Context);

        var view = await service.GetDashboardAsync(currentUserId: null, asOfDate: null);

        Assert.Equal(2, view.ShareholdingByGroup.Count);
        Assert.Equal("PERSONAL", view.ShareholdingByGroup[0].GroupName); // larger holding sorts first
        Assert.Equal(75m, view.ShareholdingByGroup[0].Percentage);
        Assert.Equal("STAFF", view.ShareholdingByGroup[1].GroupName);
        Assert.Equal(25m, view.ShareholdingByGroup[1].Percentage);
    }

    [Fact]
    public async Task GetDashboardAsync_NoCurrentUserId_LeavesMyPendingApprovalsEmpty()
    {
        using var db = new SqliteTestDb();
        var service = new DashboardService(db.Context);

        var view = await service.GetDashboardAsync(currentUserId: null, asOfDate: null);

        Assert.Empty(view.MyPendingApprovals);
    }

    [Fact]
    public async Task GetDashboardAsync_YearlyPaidUpCapital_AccumulatesAsARunningBalanceAcrossYears()
    {
        using var db = new SqliteTestDb();
        var group = TestSeed.Group(db.Context);
        var shareClass = TestSeed.ShareClass(db.Context);
        var shareholder = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001");
        var ledger = new ShareLedgerService(db.Context, new FakeCurrentUserService());
        await ledger.PostAsync(new PostLedgerEntryRequest(shareholder.Id, shareClass.Id, 1000m, 10_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2025, 6, 1)));
        await ledger.PostAsync(new PostLedgerEntryRequest(shareholder.Id, shareClass.Id, 500m, 5_000_000m, 0m, "TOP-UP", null, null, null, new DateOnly(2026, 6, 1)));
        var service = new DashboardService(db.Context);

        var view = await service.GetDashboardAsync(currentUserId: null, asOfDate: null);

        Assert.Equal(2, view.YearlyPaidUpCapital.Count);
        Assert.Equal("2025", view.YearlyPaidUpCapital[0].FinancialYear);
        Assert.Equal(10_000_000m, view.YearlyPaidUpCapital[0].ClosingPaidUpCapital);
        Assert.Equal("2026", view.YearlyPaidUpCapital[1].FinancialYear);
        Assert.Equal(15_000_000m, view.YearlyPaidUpCapital[1].ClosingPaidUpCapital); // cumulative, not a per-year delta
    }
}
