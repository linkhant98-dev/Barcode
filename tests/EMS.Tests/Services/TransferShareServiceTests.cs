using EMS.Application.Shares;
using EMS.Domain.Common;
using EMS.Infrastructure.Services;
using EMS.Tests.TestSupport;
using Xunit;

namespace EMS.Tests.Services;

/// <summary>Section 8 - trade/non-trade transfers, seller-balance validation, atomic debit/credit (8.1, 8.3).</summary>
public class TransferShareServiceTests
{
    private sealed record Fixture(TransferShareService Service, ShareLedgerService Ledger, long FromId, long ToId, long ShareClassId);

    private static Fixture Build(SqliteTestDb db, FakeCurrentUserService? user = null)
    {
        user ??= new FakeCurrentUserService { UserId = "maker" };
        var group = TestSeed.Group(db.Context);
        var shareClass = TestSeed.ShareClass(db.Context);
        var from = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001", "Seller");
        var to = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000002", "Buyer");

        var refNumbers = new ReferenceNumberService(db.Context);
        var audit = new AuditService(db.Context, user);
        var workflow = new WorkflowService(db.Context, user, audit, new FakeNotificationService());
        var ledger = new ShareLedgerService(db.Context, user);
        var service = new TransferShareService(db.Context, user, refNumbers, workflow, ledger, audit);

        return new Fixture(service, ledger, from.Id, to.Id, shareClass.Id);
    }

    private static async Task GiveSellerShares(ShareLedgerService ledger, long shareholderId, long shareClassId, decimal quantity) =>
        await ledger.PostAsync(new PostLedgerEntryRequest(shareholderId, shareClassId, quantity, quantity * 10_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));

    [Fact]
    public async Task CreateDraftAsync_FromAndToAreTheSameShareholder_Throws()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        await GiveSellerShares(f.Ledger, f.FromId, f.ShareClassId, 1000m);

        var request = new CreateTransferRequest(f.FromId, f.FromId, f.ShareClassId, new DateOnly(2026, 6, 1), 100m, TransferType.NonTrade, 0, 0, 0, 0, NonTradeReason.Gift, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CreateDraftAsync(request));
    }

    [Fact]
    public async Task CreateDraftAsync_QuantityExceedsAvailableBalance_Throws()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        await GiveSellerShares(f.Ledger, f.FromId, f.ShareClassId, 500m);

        var request = new CreateTransferRequest(f.FromId, f.ToId, f.ShareClassId, new DateOnly(2026, 6, 1), 600m, TransferType.NonTrade, 0, 0, 0, 0, NonTradeReason.Gift, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CreateDraftAsync(request));
    }

    [Fact]
    public async Task CreateDraftAsync_NonTradeWithoutAReason_Throws()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        await GiveSellerShares(f.Ledger, f.FromId, f.ShareClassId, 1000m);

        var request = new CreateTransferRequest(f.FromId, f.ToId, f.ShareClassId, new DateOnly(2026, 6, 1), 100m, TransferType.NonTrade, 0, 0, 0, 0, null, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CreateDraftAsync(request));
    }

    [Fact]
    public async Task CreateDraftAsync_ValidTradeTransfer_ComputesTotalConsideration()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        await GiveSellerShares(f.Ledger, f.FromId, f.ShareClassId, 1000m);

        var request = new CreateTransferRequest(f.FromId, f.ToId, f.ShareClassId, new DateOnly(2026, 6, 1), 300m, TransferType.Trade, 3_000_000m, 300_000m, 3_300_000m, 0, null, null);
        var transaction = await f.Service.CreateDraftAsync(request);

        Assert.Equal(3_300_000m, transaction.ShareTransfer!.TotalConsideration);
        Assert.Equal(WorkflowStatus.Draft, transaction.Status);
    }

    [Fact]
    public async Task CreateDraftAsync_NonTradeTransfer_ZerosOutTradeMoneyFields()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        await GiveSellerShares(f.Ledger, f.FromId, f.ShareClassId, 1000m);

        // Trade money fields are supplied but the transfer type is non-trade - they must not leak through.
        var request = new CreateTransferRequest(f.FromId, f.ToId, f.ShareClassId, new DateOnly(2026, 6, 1), 100m, TransferType.NonTrade, 1_000_000m, 0, 1_000_000m, 0, NonTradeReason.Inheritance, "Estate transfer");
        var transaction = await f.Service.CreateDraftAsync(request);

        Assert.Equal(0m, transaction.ShareTransfer!.CapitalAmount);
        Assert.Equal(0m, transaction.ShareTransfer.TotalConsideration);
        Assert.Equal(NonTradeReason.Inheritance, transaction.ShareTransfer.NonTradeReason);
    }

    [Fact]
    public async Task PostApprovedAsync_DebitsTheSellerAndCreditsTheBuyerAtomically()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        await GiveSellerShares(f.Ledger, f.FromId, f.ShareClassId, 1000m);

        var request = new CreateTransferRequest(f.FromId, f.ToId, f.ShareClassId, new DateOnly(2026, 6, 1), 400m, TransferType.NonTrade, 0, 0, 0, 0, NonTradeReason.Gift, null);
        var transaction = await f.Service.CreateDraftAsync(request);

        await f.Service.PostApprovedAsync(transaction.Id);

        Assert.Equal(600m, await f.Ledger.GetCurrentBalanceAsync(f.FromId, f.ShareClassId));
        Assert.Equal(400m, await f.Ledger.GetCurrentBalanceAsync(f.ToId, f.ShareClassId));

        var reloaded = db.Context.ShareTransactions.Single(t => t.Id == transaction.Id);
        Assert.Equal(WorkflowStatus.Completed, reloaded.Status);
    }

    [Fact]
    public async Task PostApprovedAsync_BalanceDroppedBelowQuantitySincePosting_Throws()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        await GiveSellerShares(f.Ledger, f.FromId, f.ShareClassId, 1000m);

        var request = new CreateTransferRequest(f.FromId, f.ToId, f.ShareClassId, new DateOnly(2026, 6, 1), 900m, TransferType.NonTrade, 0, 0, 0, 0, NonTradeReason.Gift, null);
        var transaction = await f.Service.CreateDraftAsync(request);

        // Balance changes after the draft was created but before it was posted (e.g. another transfer posted first).
        await f.Ledger.PostAsync(new PostLedgerEntryRequest(f.FromId, f.ShareClassId, -600m, -6_000_000m, 0m, "OTHER-TRANSFER", null, null, null, new DateOnly(2026, 6, 2)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.PostApprovedAsync(transaction.Id));
    }
}
