using EMS.Application.CorporateActions;
using EMS.Application.Shares;
using EMS.Domain.Common;
using EMS.Infrastructure.Services;
using EMS.Tests.TestSupport;
using Xunit;

namespace EMS.Tests.Services;

/// <summary>Section 9 - ratio-based bonus allotment with a cash-remainder rate (9.1..9.3).</summary>
public class BonusServiceTests
{
    private sealed record Fixture(BonusService Service, ShareLedgerService Ledger, long ShareholderId, long ShareClassId);

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
        var service = new BonusService(db.Context, user, refNumbers, workflow, ledger, audit);

        return new Fixture(service, ledger, shareholder.Id, shareClass.Id);
    }

    [Fact]
    public async Task CreateEventAsync_DuplicateFinancialYearAndRecordDate_Throws()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        var request = new CreateBonusEventRequest("2025-2026", new DateOnly(2026, 6, 30), 1, 6, 5000m, f.ShareClassId);
        await f.Service.CreateEventAsync(request);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CreateEventAsync(request));
    }

    [Fact]
    public async Task CreateEventAsync_ValidRequest_AssignsAVersionedBatchTag()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        var id = await f.Service.CreateEventAsync(new CreateBonusEventRequest("2025-2026", new DateOnly(2026, 6, 30), 1, 6, 5000m, f.ShareClassId));

        var bonusEvent = db.Context.BonusEvents.Single(b => b.Id == id);
        Assert.Equal("BonusShare-20260630-v1.0", bonusEvent.BatchVersion);
        Assert.Equal(WorkflowStatus.Draft, bonusEvent.Status);
    }

    [Fact]
    public async Task PreviewCalculationAsync_MatchesTheDomainCalculatorForEachEligibleHolder()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        await f.Ledger.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, 12500m, 125_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));

        var eventId = await f.Service.CreateEventAsync(new CreateBonusEventRequest("2025-2026", new DateOnly(2026, 6, 30), 1, 6, 5000m, f.ShareClassId));
        await f.Service.PreviewCalculationAsync(eventId);

        var entitlement = Assert.Single(db.Context.BonusEntitlements);
        Assert.Equal(12500m, entitlement.EligibleShares);
        Assert.Equal(2083m, entitlement.BonusShares); // matches BonusCalculatorTests' spec example
        Assert.Equal(2m, entitlement.RemainderShares);
        Assert.Equal(10000m, entitlement.CashBonusAmount);
        Assert.False(entitlement.IsPosted);
    }

    [Fact]
    public async Task PreviewCalculationAsync_ShareholderWithNoHoldings_IsExcluded()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        // No ledger postings at all for this shareholder.
        var eventId = await f.Service.CreateEventAsync(new CreateBonusEventRequest("2025-2026", new DateOnly(2026, 6, 30), 1, 6, 5000m, f.ShareClassId));

        await f.Service.PreviewCalculationAsync(eventId);

        Assert.Empty(db.Context.BonusEntitlements);
    }

    [Fact]
    public async Task PreviewCalculationAsync_RunTwice_ReplacesThePreviousSnapshotRatherThanDuplicatingIt()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        await f.Ledger.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, 1000m, 10_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        var eventId = await f.Service.CreateEventAsync(new CreateBonusEventRequest("2025-2026", new DateOnly(2026, 6, 30), 1, 6, 5000m, f.ShareClassId));

        await f.Service.PreviewCalculationAsync(eventId);
        await f.Service.PreviewCalculationAsync(eventId);

        Assert.Single(db.Context.BonusEntitlements);
    }

    [Fact]
    public async Task PostApprovedAsync_PostsALedgerEntryPerUnpostedEntitlementAndCompletesTheEvent()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        await f.Ledger.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, 12000m, 120_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        var eventId = await f.Service.CreateEventAsync(new CreateBonusEventRequest("2025-2026", new DateOnly(2026, 6, 30), 1, 6, 5000m, f.ShareClassId));
        await f.Service.PreviewCalculationAsync(eventId);

        await f.Service.PostApprovedAsync(eventId);

        var balance = await f.Ledger.GetCurrentBalanceAsync(f.ShareholderId, f.ShareClassId);
        Assert.Equal(14000m, balance); // 12000 opening + 2000 bonus (12000/6, no remainder)

        var entitlement = db.Context.BonusEntitlements.Single();
        Assert.True(entitlement.IsPosted);
        Assert.Equal("Posted", entitlement.SettlementStatus);

        var bonusEvent = db.Context.BonusEvents.Single(b => b.Id == eventId);
        Assert.Equal(WorkflowStatus.Completed, bonusEvent.Status);
        Assert.NotNull(bonusEvent.PostedDate);
    }

    [Fact]
    public async Task PostApprovedAsync_RemainderShares_MarksSettlementAsCashBonusPending()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        await f.Ledger.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, 12500m, 125_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        var eventId = await f.Service.CreateEventAsync(new CreateBonusEventRequest("2025-2026", new DateOnly(2026, 6, 30), 1, 6, 5000m, f.ShareClassId));
        await f.Service.PreviewCalculationAsync(eventId);

        await f.Service.PostApprovedAsync(eventId);

        var entitlement = db.Context.BonusEntitlements.Single();
        Assert.Equal("CashBonusPending", entitlement.SettlementStatus);
    }
}
