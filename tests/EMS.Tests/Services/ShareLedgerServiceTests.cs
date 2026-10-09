using EMS.Application.Shares;
using EMS.Infrastructure.Services;
using EMS.Tests.TestSupport;
using Xunit;

namespace EMS.Tests.Services;

/// <summary>16.1 / 13.1.1 - the ledger is the single source of truth; running balances must chain correctly
/// across postings and reversed entries must be excluded from balance queries.</summary>
public class ShareLedgerServiceTests
{
    private static (ShareLedgerService Service, long ShareholderId, long ShareClassId, long OtherShareClassId) Build(SqliteTestDb db)
    {
        var group = TestSeed.Group(db.Context);
        var shareClass = TestSeed.ShareClass(db.Context);
        var otherShareClass = TestSeed.ShareClass(db.Context, "PREF");
        var shareholder = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001");
        var service = new ShareLedgerService(db.Context, new FakeCurrentUserService());

        return (service, shareholder.Id, shareClass.Id, otherShareClass.Id);
    }

    [Fact]
    public async Task PostAsync_FirstEntryForAShareholder_RunningBalanceEqualsTheDelta()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);

        await f.Service.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, 8200m, 82_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));

        var entry = Assert.Single(db.Context.ShareLedgerEntries);
        Assert.Equal(8200m, entry.RunningQuantityBalance);
        Assert.Equal(82_000_000m, entry.RunningPaidUpCapital);
    }

    [Fact]
    public async Task PostAsync_SecondEntry_ChainsFromThePreviousRunningBalance_NotFromASum()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);

        await f.Service.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, 8200m, 82_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        await f.Service.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, 500m, 5_000_000m, 0m, "TOP-UP", null, null, null, new DateOnly(2026, 2, 1)));

        var balance = await f.Service.GetCurrentBalanceAsync(f.ShareholderId, f.ShareClassId);
        Assert.Equal(8700m, balance);
    }

    [Fact]
    public async Task PostAsync_NegativeDelta_DecreasesTheRunningBalance()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);

        await f.Service.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, 1000m, 10_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        await f.Service.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, -300m, -3_000_000m, 0m, "TRANSFER-OUT", null, null, null, new DateOnly(2026, 2, 1)));

        var balance = await f.Service.GetCurrentBalanceAsync(f.ShareholderId, f.ShareClassId);
        Assert.Equal(700m, balance);
    }

    [Fact]
    public async Task GetCurrentBalanceAsync_DifferentShareClassesAreIndependent()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);

        await f.Service.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, 1000m, 10_000_000m, 0m, "OPENING-ORD", null, null, null, new DateOnly(2026, 1, 1)));
        await f.Service.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.OtherShareClassId, 500m, 5_000_000m, 0m, "OPENING-PREF", null, null, null, new DateOnly(2026, 1, 1)));

        Assert.Equal(1000m, await f.Service.GetCurrentBalanceAsync(f.ShareholderId, f.ShareClassId));
        Assert.Equal(500m, await f.Service.GetCurrentBalanceAsync(f.ShareholderId, f.OtherShareClassId));
    }

    [Fact]
    public async Task GetCurrentBalanceAsync_AsOfADateBeforeALaterPosting_ExcludesIt()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);

        await f.Service.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, 1000m, 10_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        await f.Service.PostAsync(new PostLedgerEntryRequest(f.ShareholderId, f.ShareClassId, 500m, 5_000_000m, 0m, "TOP-UP", null, null, null, new DateOnly(2026, 6, 1)));

        var balanceInMarch = await f.Service.GetCurrentBalanceAsync(f.ShareholderId, f.ShareClassId, new DateOnly(2026, 3, 1));
        Assert.Equal(1000m, balanceInMarch);
    }
}
