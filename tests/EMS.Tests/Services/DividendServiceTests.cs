using EMS.Application.CorporateActions;
using EMS.Application.Shares;
using EMS.Domain.Common;
using EMS.Infrastructure.Services;
using EMS.Tests.TestSupport;
using Xunit;

namespace EMS.Tests.Services;

/// <summary>Section 10 - old/new share proration and settlement tracking (10.1..10.3).</summary>
public class DividendServiceTests
{
    private sealed record Fixture(DividendService Service, ShareLedgerService Ledger, long ShareholderId, long ShareClassId);

    private static Fixture Build(SqliteTestDb db)
    {
        var user = new FakeCurrentUserService { UserId = "maker" };
        var group = TestSeed.Group(db.Context);
        var shareClass = TestSeed.ShareClass(db.Context);
        var shareholder = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001");

        var refNumbers = new ReferenceNumberService(db.Context);
        var audit = new AuditService(db.Context, user);
        var workflow = new WorkflowService(db.Context, user, audit, new FakeNotificationService());
        var ledger = new ShareLedgerService(db.Context, user);
        var service = new DividendService(db.Context, user, refNumbers, workflow, audit);

        return new Fixture(service, ledger, shareholder.Id, shareClass.Id);
    }

    [Fact]
    public async Task PreviewCalculationAsync_SharesHeldSinceBeforeTheYear_AreTreatedAsOldSharesWithNoProration()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        await f.Ledger.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, 1000m, 10_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2024, 1, 10)));

        var eventId = await f.Service.CreateEventAsync(new CreateDividendEventRequest("2025-2026", new DateOnly(2026, 6, 30), 8m, 10_000m));
        await f.Service.PreviewCalculationAsync(eventId);

        var entitlement = Assert.Single(db.Context.DividendEntitlements);
        Assert.Equal(1000m, entitlement.OldShares);
        Assert.Equal(0m, entitlement.NewShares);
        Assert.Equal(800_000m, entitlement.TotalDividend); // 1000 * 10,000 * 8%
        Assert.Equal(800_000m, entitlement.OutstandingBalance);
    }

    [Fact]
    public async Task PreviewCalculationAsync_SharesAcquiredDuringTheYear_AreProratedByEligibleDays()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        var recordDate = new DateOnly(2026, 6, 30);
        var acquiredDate = new DateOnly(2026, 2, 1);
        await f.Ledger.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, 1000m, 10_000_000m, 0m, "TOP-UP", null, null, null, acquiredDate));

        var eventId = await f.Service.CreateEventAsync(new CreateDividendEventRequest("2025-2026", recordDate, 8m, 10_000m));
        await f.Service.PreviewCalculationAsync(eventId);

        var entitlement = Assert.Single(db.Context.DividendEntitlements);
        var expectedDays = recordDate.DayNumber - acquiredDate.DayNumber;
        Assert.Equal(0m, entitlement.OldShares);
        Assert.Equal(1000m, entitlement.NewShares);
        Assert.Equal(expectedDays, entitlement.EligibleDaysForNewShares);
        var expectedDividend = 1000m * 10_000m * 8m / 100m * expectedDays / 365m;
        Assert.Equal(expectedDividend, entitlement.TotalDividend);
    }

    [Fact]
    public async Task PreviewCalculationAsync_OldAndNewSharesTogether_SumsBothComponents()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        await f.Ledger.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, 1000m, 10_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2024, 1, 10)));
        await f.Ledger.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, 200m, 2_000_000m, 0m, "TOP-UP", null, null, null, new DateOnly(2026, 3, 1)));

        var eventId = await f.Service.CreateEventAsync(new CreateDividendEventRequest("2025-2026", new DateOnly(2026, 6, 30), 8m, 10_000m));
        await f.Service.PreviewCalculationAsync(eventId);

        var entitlement = Assert.Single(db.Context.DividendEntitlements);
        Assert.Equal(1000m, entitlement.OldShares);
        Assert.Equal(200m, entitlement.NewShares);
        Assert.Equal(entitlement.OldShareDividend + entitlement.NewShareDividend, entitlement.TotalDividend);
    }

    [Fact]
    public async Task SettleAsync_TotalExceedsTheEntitlement_Throws()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        await f.Ledger.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, 1000m, 10_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2024, 1, 10)));
        var eventId = await f.Service.CreateEventAsync(new CreateDividendEventRequest("2025-2026", new DateOnly(2026, 6, 30), 8m, 10_000m));
        await f.Service.PreviewCalculationAsync(eventId);
        var entitlement = db.Context.DividendEntitlements.Single();

        var request = new SettleDividendRequest(entitlement.Id, CashWithdrawal: 900_000m, AccountTransfer: 0, ReinvestedAmount: 0, SettlementReference: null); // entitlement is 800,000

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SettleAsync(request));
    }

    [Fact]
    public async Task SettleAsync_PartialCashWithdrawal_UpdatesOutstandingAndRecordsASettlementRow()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        await f.Ledger.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, 1000m, 10_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2024, 1, 10)));
        var eventId = await f.Service.CreateEventAsync(new CreateDividendEventRequest("2025-2026", new DateOnly(2026, 6, 30), 8m, 10_000m));
        await f.Service.PreviewCalculationAsync(eventId);
        var entitlement = db.Context.DividendEntitlements.Single();

        await f.Service.SettleAsync(new SettleDividendRequest(entitlement.Id, CashWithdrawal: 300_000m, AccountTransfer: 0, ReinvestedAmount: 0, SettlementReference: "REF-1"));

        var reloaded = db.Context.DividendEntitlements.Single();
        Assert.Equal(300_000m, reloaded.CashWithdrawal);
        Assert.Equal(500_000m, reloaded.OutstandingBalance); // 800,000 - 300,000
        var settlement = Assert.Single(db.Context.DividendSettlements);
        Assert.Equal(DividendSettlementMethod.CashWithdrawal, settlement.Method);
        Assert.Equal(300_000m, settlement.Amount);
    }

    [Fact]
    public async Task PostApprovedAsync_CompletesTheEventWithAPostedDate()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        var eventId = await f.Service.CreateEventAsync(new CreateDividendEventRequest("2025-2026", new DateOnly(2026, 6, 30), 8m, 10_000m));

        await f.Service.PostApprovedAsync(eventId);

        var dividendEvent = db.Context.DividendEvents.Single(d => d.Id == eventId);
        Assert.Equal(WorkflowStatus.Completed, dividendEvent.Status);
        Assert.NotNull(dividendEvent.PostedDate);
    }
}
