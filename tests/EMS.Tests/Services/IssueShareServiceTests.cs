using EMS.Application.Shares;
using EMS.Domain.Common;
using EMS.Infrastructure.Services;
using EMS.Tests.TestSupport;
using Xunit;

namespace EMS.Tests.Services;

/// <summary>Section 7, business rules IS-BR-01..08.</summary>
public class IssueShareServiceTests
{
    private static (IssueShareService Service, EMS.Domain.Shareholders.Shareholder Shareholder, long ShareClassId) Build(
        SqliteTestDb db, FakeCurrentUserService? user = null, ShareholderStatus status = ShareholderStatus.Active, KycResult kycStatus = KycResult.Approved)
    {
        user ??= new FakeCurrentUserService { UserId = "maker" };
        var group = TestSeed.Group(db.Context);
        var shareClass = TestSeed.ShareClass(db.Context);
        var shareholder = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001", status: status, kycStatus: kycStatus);

        var refNumbers = new ReferenceNumberService(db.Context);
        var audit = new AuditService(db.Context, user);
        var notifications = new FakeNotificationService();
        var workflow = new WorkflowService(db.Context, user, audit, notifications);
        var ledger = new ShareLedgerService(db.Context, user);
        var service = new IssueShareService(db.Context, user, refNumbers, workflow, ledger, audit);

        return (service, shareholder, shareClass.Id);
    }

    private static CreateIssueRequest ValidRequest(long shareholderId, long shareClassId, decimal shares = 1000m, decimal capitalPerShare = 10_000m, decimal premiumPerShare = 0m) =>
        new(IssueApplyType.IssueShare, shareholderId, shareClassId, new DateOnly(2026, 6, 1), shares, capitalPerShare, premiumPerShare,
            CashAmount: shares * capitalPerShare + shares * premiumPerShare, ChequeAmount: 0, ChequeNumber: null, ChequeDate: null, DividendEntitlementId: null);

    [Fact]
    public async Task CreateDraftAsync_ValidRequest_CalculatesCapitalPremiumAndTotal()
    {
        using var db = new SqliteTestDb();
        var (service, shareholder, shareClassId) = Build(db);

        var transaction = await service.CreateDraftAsync(ValidRequest(shareholder.Id, shareClassId, shares: 1000m, capitalPerShare: 10_000m, premiumPerShare: 2_000m));

        Assert.Equal(10_000_000m, transaction.ShareIssue!.CapitalAmount);
        Assert.Equal(2_000_000m, transaction.ShareIssue.PremiumAmount);
        Assert.Equal(12_000_000m, transaction.ShareIssue.TotalAmount);
        Assert.Equal(WorkflowStatus.Draft, transaction.Status);
        Assert.StartsWith("IS-", transaction.TransactionNo);
    }

    [Fact]
    public async Task CreateDraftAsync_ShareholderNotActive_Throws()
    {
        using var db = new SqliteTestDb();
        var (service, shareholder, shareClassId) = Build(db, status: ShareholderStatus.Suspended);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDraftAsync(ValidRequest(shareholder.Id, shareClassId)));
    }

    [Fact]
    public async Task CreateDraftAsync_ShareholderKycNotApproved_Throws()
    {
        using var db = new SqliteTestDb();
        var (service, shareholder, shareClassId) = Build(db, kycStatus: KycResult.Pending);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDraftAsync(ValidRequest(shareholder.Id, shareClassId)));
    }

    [Fact]
    public async Task CreateDraftAsync_ZeroShares_Throws()
    {
        using var db = new SqliteTestDb();
        var (service, shareholder, shareClassId) = Build(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDraftAsync(ValidRequest(shareholder.Id, shareClassId, shares: 0)));
    }

    [Fact]
    public async Task CreateDraftAsync_IssueDateBeforeRegistrationDate_Throws()
    {
        using var db = new SqliteTestDb();
        var (service, shareholder, shareClassId) = Build(db);
        var request = ValidRequest(shareholder.Id, shareClassId) with { IssueDate = new DateOnly(2020, 1, 1) };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDraftAsync(request));
    }

    [Fact]
    public async Task CreateDraftAsync_PaymentTotalDoesNotMatchSubscriptionTotal_Throws()
    {
        using var db = new SqliteTestDb();
        var (service, shareholder, shareClassId) = Build(db);
        var request = ValidRequest(shareholder.Id, shareClassId, shares: 1000m, capitalPerShare: 10_000m) with { CashAmount = 1_000_000m }; // short of the 10,000,000 owed

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDraftAsync(request));
    }

    [Fact]
    public async Task SubmitAsync_MovesTheTransactionToPendingApproval()
    {
        using var db = new SqliteTestDb();
        TestSeed.SingleStepRule(db.Context, "IS", "DGM Approver");
        var (service, shareholder, shareClassId) = Build(db);
        var transaction = await service.CreateDraftAsync(ValidRequest(shareholder.Id, shareClassId));

        await service.SubmitAsync(transaction.Id);

        var reloaded = db.Context.ShareTransactions.Single(t => t.Id == transaction.Id);
        Assert.Equal(WorkflowStatus.PendingApproval, reloaded.Status);
        Assert.NotNull(reloaded.ApprovalInstanceId);
    }

    [Fact]
    public async Task PostApprovedAsync_PostsALedgerEntryAndIssuesAPendingPrintCertificate()
    {
        using var db = new SqliteTestDb();
        var (service, shareholder, shareClassId) = Build(db);
        var transaction = await service.CreateDraftAsync(ValidRequest(shareholder.Id, shareClassId, shares: 1000m, capitalPerShare: 10_000m));

        await service.PostApprovedAsync(transaction.Id);

        var ledgerEntry = Assert.Single(db.Context.ShareLedgerEntries);
        Assert.Equal(1000m, ledgerEntry.QuantityDelta);
        Assert.Equal(10_000_000m, ledgerEntry.CapitalAmountDelta);

        var certificate = Assert.Single(db.Context.ShareCertificates);
        Assert.Equal(CertificateStatus.PendingPrint, certificate.Status);
        Assert.Equal(1000m, certificate.Quantity);

        var reloaded = db.Context.ShareTransactions.Single(t => t.Id == transaction.Id);
        Assert.Equal(WorkflowStatus.Completed, reloaded.Status);
    }
}
