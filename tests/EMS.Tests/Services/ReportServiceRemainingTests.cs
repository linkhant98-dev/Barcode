using EMS.Application.CorporateActions;
using EMS.Application.Reporting;
using EMS.Application.Shareholders;
using EMS.Application.Shares;
using EMS.Domain.Common;
using EMS.Domain.CorporateActions;
using EMS.Domain.Reporting;
using EMS.Domain.Shares;
using EMS.Domain.Workflow;
using EMS.Infrastructure.Identity;
using EMS.Infrastructure.Services;
using EMS.Tests.TestSupport;
using Xunit;

namespace EMS.Tests.Services;

/// <summary>Section 13.7/13.8 - RPT-003 through RPT-016, the reports not already covered by
/// ReportServiceTests (which owns RPT-001/RPT-002). One happy-path assertion per report: the row shape and
/// any total/aggregate the report computes.</summary>
public class ReportServiceRemainingTests
{
    private static void SeedDefinition(SqliteTestDb db, string code, string name = "Report")
    {
        db.Context.ReportDefinitions.Add(new ReportDefinition { ReportCode = code, NameEn = name, NameMm = name, Category = "Test" });
        db.Context.SaveChanges();
    }

    private static ReportService Service(SqliteTestDb db)
    {
        var user = new FakeCurrentUserService();
        return new ReportService(db.Context, user, new PermissionService(db.Context, user));
    }

    [Fact]
    public async Task RunAsync_SharesAndCertificateRecord_ListsCertificatesWithTheQuantityTotal()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-003");
        var group = TestSeed.Group(db.Context);
        var shareClass = TestSeed.ShareClass(db.Context);
        var shareholder = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001");
        db.Context.ShareCertificates.Add(new ShareCertificate
        {
            CertificateNumber = "CERT-0001",
            ShareholderId = shareholder.Id,
            ShareClassId = shareClass.Id,
            Quantity = 500m,
            Status = CertificateStatus.Printed,
            IssueDate = new DateOnly(2026, 1, 1),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system"
        });
        db.Context.SaveChanges();

        var result = await Service(db).RunAsync("RPT-003", new ReportParameters());

        var row = Assert.Single(result.Rows);
        Assert.Equal("CERT-0001", row["CertificateNumber"]);
        Assert.Equal(500m, result.Totals!["Quantity"]);
    }

    [Fact]
    public async Task RunAsync_PaidUpCapitalRecord_ReflectsTheLedgersRunningCapitalBalance()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-004");
        var group = TestSeed.Group(db.Context);
        var shareClass = TestSeed.ShareClass(db.Context);
        var shareholder = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001");
        var ledger = new ShareLedgerService(db.Context, new FakeCurrentUserService());
        await ledger.PostAsync(new PostLedgerEntryRequest(shareholder.Id, shareClass.Id, 1000m, 10_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));

        var result = await Service(db).RunAsync("RPT-004", new ReportParameters());

        var row = Assert.Single(result.Rows);
        Assert.Equal(10_000_000m, row["RunningCapital"]);
        Assert.Equal(10_000_000m, result.Totals!["CapitalAmount"]);
    }

    [Fact]
    public async Task RunAsync_TransferRecord_ListsOnlyTransferTypeTransactionsWithTheQuantityTotal()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-005");
        var group = TestSeed.Group(db.Context);
        var shareClass = TestSeed.ShareClass(db.Context);
        var from = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001", "Seller");
        var to = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000002", "Buyer");
        db.Context.ShareTransactions.Add(new ShareTransaction
        {
            TransactionNo = "TS-2026-000001",
            Type = ShareTransactionType.TransferShares,
            Status = WorkflowStatus.Completed,
            EffectiveDate = new DateOnly(2026, 6, 1),
            MakerUserId = "maker",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system",
            ShareTransfer = new ShareTransfer { FromShareholderId = from.Id, ToShareholderId = to.Id, ShareClassId = shareClass.Id, Quantity = 300m, TransferType = TransferType.NonTrade }
        });
        db.Context.SaveChanges();

        var result = await Service(db).RunAsync("RPT-005", new ReportParameters());

        var row = Assert.Single(result.Rows);
        Assert.Equal(from.ShareholderNo, row["From"]);
        Assert.Equal(to.ShareholderNo, row["To"]);
        Assert.Equal(300m, result.Totals!["Quantity"]);
    }

    [Fact]
    public async Task RunAsync_BonusShares_ListsEntitlementsWithTheBonusSharesTotal()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-006");
        var group = TestSeed.Group(db.Context);
        var shareClass = TestSeed.ShareClass(db.Context);
        var shareholder = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001");
        var user = new FakeCurrentUserService { UserId = "maker" };
        var refNumbers = new ReferenceNumberService(db.Context);
        var audit = new AuditService(db.Context, user);
        var workflow = new WorkflowService(db.Context, user, audit, new FakeNotificationService());
        var ledger = new ShareLedgerService(db.Context, user);
        var bonus = new BonusService(db.Context, user, refNumbers, workflow, ledger, audit);
        await ledger.PostAsync(new PostLedgerEntryRequest(shareholder.Id, shareClass.Id, 12500m, 125_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        var eventId = await bonus.CreateEventAsync(new CreateBonusEventRequest("2025-2026", new DateOnly(2026, 6, 30), 1, 6, 5000m, shareClass.Id));
        await bonus.PreviewCalculationAsync(eventId);

        var result = await Service(db).RunAsync("RPT-006", new ReportParameters());

        var row = Assert.Single(result.Rows);
        Assert.Equal(2083m, row["BonusShares"]);
        Assert.Equal(2083m, result.Totals!["BonusShares"]);
    }

    [Fact]
    public async Task RunAsync_CashBonusSummary_OnlyIncludesEntitlementsWithACashRemainder()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-007");
        var group = TestSeed.Group(db.Context);
        var shareClass = TestSeed.ShareClass(db.Context);
        var withRemainder = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001", "Has-Remainder");
        var noRemainder = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000002", "No-Remainder");
        var user = new FakeCurrentUserService { UserId = "maker" };
        var refNumbers = new ReferenceNumberService(db.Context);
        var audit = new AuditService(db.Context, user);
        var workflow = new WorkflowService(db.Context, user, audit, new FakeNotificationService());
        var ledger = new ShareLedgerService(db.Context, user);
        var bonus = new BonusService(db.Context, user, refNumbers, workflow, ledger, audit);
        await ledger.PostAsync(new PostLedgerEntryRequest(withRemainder.Id, shareClass.Id, 12500m, 125_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        await ledger.PostAsync(new PostLedgerEntryRequest(noRemainder.Id, shareClass.Id, 12000m, 120_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        var eventId = await bonus.CreateEventAsync(new CreateBonusEventRequest("2025-2026", new DateOnly(2026, 6, 30), 1, 6, 5000m, shareClass.Id));
        await bonus.PreviewCalculationAsync(eventId);

        var result = await Service(db).RunAsync("RPT-007", new ReportParameters());

        var row = Assert.Single(result.Rows);
        Assert.Equal(withRemainder.ShareholderNo, row["Shareholder"]);
        Assert.Equal(10000m, result.Totals!["CashBonus"]);
    }

    [Fact]
    public async Task RunAsync_DividendSummary_FiltersByFinancialYearWhenSupplied()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-008");
        var group = TestSeed.Group(db.Context);
        var shareClass = TestSeed.ShareClass(db.Context);
        var shareholder = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001");
        var user = new FakeCurrentUserService { UserId = "maker" };
        var refNumbers = new ReferenceNumberService(db.Context);
        var audit = new AuditService(db.Context, user);
        var workflow = new WorkflowService(db.Context, user, audit, new FakeNotificationService());
        var dividend = new DividendService(db.Context, user, refNumbers, workflow, audit);
        var ledger = new ShareLedgerService(db.Context, user);
        await ledger.PostAsync(new PostLedgerEntryRequest(shareholder.Id, shareClass.Id, 1000m, 10_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2024, 1, 1)));
        var eventId = await dividend.CreateEventAsync(new CreateDividendEventRequest("2025-2026", new DateOnly(2026, 6, 30), 8m, 10_000m));
        await dividend.PreviewCalculationAsync(eventId);

        var matching = await Service(db).RunAsync("RPT-008", new ReportParameters(FinancialYear: "2025-2026"));
        var nonMatching = await Service(db).RunAsync("RPT-008", new ReportParameters(FinancialYear: "2099-2100"));

        Assert.Single(matching.Rows);
        Assert.Empty(nonMatching.Rows);
    }

    [Fact]
    public async Task RunAsync_DividendByPerson_WithoutAShareholderId_ReturnsNoRows()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-009");

        var result = await Service(db).RunAsync("RPT-009", new ReportParameters());

        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task RunAsync_DividendByPerson_WithAShareholderId_ReturnsOnlyThatShareholdersEntitlements()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-009");
        var group = TestSeed.Group(db.Context);
        var shareClass = TestSeed.ShareClass(db.Context);
        var shareholder = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001");
        var user = new FakeCurrentUserService { UserId = "maker" };
        var refNumbers = new ReferenceNumberService(db.Context);
        var audit = new AuditService(db.Context, user);
        var workflow = new WorkflowService(db.Context, user, audit, new FakeNotificationService());
        var dividend = new DividendService(db.Context, user, refNumbers, workflow, audit);
        var ledger = new ShareLedgerService(db.Context, user);
        await ledger.PostAsync(new PostLedgerEntryRequest(shareholder.Id, shareClass.Id, 1000m, 10_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2024, 1, 1)));
        var eventId = await dividend.CreateEventAsync(new CreateDividendEventRequest("2025-2026", new DateOnly(2026, 6, 30), 8m, 10_000m));
        await dividend.PreviewCalculationAsync(eventId);

        var result = await Service(db).RunAsync("RPT-009", new ReportParameters(ShareholderId: shareholder.Id));

        var row = Assert.Single(result.Rows);
        Assert.Equal(800_000m, row["TotalDividend"]);
    }

    [Fact]
    public async Task RunAsync_DividendRecordByYear_SumsProvisionAndOutstandingAcrossHolders()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-010");
        var group = TestSeed.Group(db.Context);
        var shareClass = TestSeed.ShareClass(db.Context);
        var a = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001", "Alice");
        var b = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000002", "Bob");
        var user = new FakeCurrentUserService { UserId = "maker" };
        var refNumbers = new ReferenceNumberService(db.Context);
        var audit = new AuditService(db.Context, user);
        var workflow = new WorkflowService(db.Context, user, audit, new FakeNotificationService());
        var dividend = new DividendService(db.Context, user, refNumbers, workflow, audit);
        var ledger = new ShareLedgerService(db.Context, user);
        await ledger.PostAsync(new PostLedgerEntryRequest(a.Id, shareClass.Id, 1000m, 10_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2024, 1, 1)));
        await ledger.PostAsync(new PostLedgerEntryRequest(b.Id, shareClass.Id, 500m, 5_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2024, 1, 1)));
        var eventId = await dividend.CreateEventAsync(new CreateDividendEventRequest("2025-2026", new DateOnly(2026, 6, 30), 8m, 10_000m));
        await dividend.PreviewCalculationAsync(eventId);

        var result = await Service(db).RunAsync("RPT-010", new ReportParameters());

        var row = Assert.Single(result.Rows);
        Assert.Equal("2025-2026", row["Year"]);
        Assert.Equal(1_200_000m, row["TotalProvision"]); // (1000+500) * 10,000 * 8%
    }

    [Fact]
    public async Task RunAsync_ApprovalHistory_ListsOnlyStepsWithARecordedDecision()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-011");
        var instance = new ApprovalInstance
        {
            EntityType = "ShareTransaction",
            EntityId = 1,
            EntityReference = "IS-2026-000001",
            Status = WorkflowStatus.Completed,
            SubmittedByUserId = "maker",
            SubmittedAtUtc = DateTime.UtcNow.AddDays(-2),
            CurrentSequence = 2,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system"
        };
        instance.Steps.Add(new ApprovalStep { Sequence = 1, StepName = "DGM", ApproverRole = "DGM Approver", Status = ApprovalStepStatus.Approved, Decision = ApprovalDecision.Approve, DecisionByUserId = "dgm1", DecisionAtUtc = DateTime.UtcNow.AddDays(-1), CreatedAtUtc = DateTime.UtcNow, CreatedBy = "system" });
        instance.Steps.Add(new ApprovalStep { Sequence = 2, StepName = "Legal", ApproverRole = "Legal Approver", Status = ApprovalStepStatus.Waiting, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "system" });
        db.Context.ApprovalInstances.Add(instance);
        db.Context.SaveChanges();

        var result = await Service(db).RunAsync("RPT-011", new ReportParameters());

        var row = Assert.Single(result.Rows);
        Assert.Equal("DGM", row["Step"]);
        Assert.Equal("Approve", row["Decision"]);
    }

    [Fact]
    public async Task RunAsync_PendingApprovalAging_OrdersOldestFirstByAgeDescending()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-012");
        var older = new ApprovalInstance
        {
            EntityType = "ShareTransaction",
            EntityId = 1,
            EntityReference = "IS-2026-000001",
            Status = WorkflowStatus.PendingApproval,
            SubmittedByUserId = "maker",
            SubmittedAtUtc = DateTime.UtcNow.AddDays(-10),
            CurrentSequence = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system"
        };
        older.Steps.Add(new ApprovalStep { Sequence = 1, StepName = "DGM", ApproverRole = "DGM Approver", Status = ApprovalStepStatus.Pending, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "system" });
        var newer = new ApprovalInstance
        {
            EntityType = "ShareTransaction",
            EntityId = 2,
            EntityReference = "IS-2026-000002",
            Status = WorkflowStatus.PendingApproval,
            SubmittedByUserId = "maker",
            SubmittedAtUtc = DateTime.UtcNow.AddDays(-1),
            CurrentSequence = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system"
        };
        newer.Steps.Add(new ApprovalStep { Sequence = 1, StepName = "DGM", ApproverRole = "DGM Approver", Status = ApprovalStepStatus.Pending, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "system" });
        db.Context.ApprovalInstances.AddRange(older, newer);
        db.Context.SaveChanges();

        var result = await Service(db).RunAsync("RPT-012", new ReportParameters());

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("IS-2026-000001", result.Rows[0]["Reference"]); // older step sorts first (larger AgeDays)
    }

    [Fact]
    public async Task RunAsync_KycStatusAndAging_ReflectsTheCasesResultAndDecisionUser()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-013");
        var group = TestSeed.Group(db.Context);
        var user = new FakeCurrentUserService { UserId = "maker" };
        var refNumbers = new ReferenceNumberService(db.Context);
        var audit = new AuditService(db.Context, user);
        var notifications = new FakeNotificationService();
        var registration = new ShareholderRegistrationService(db.Context, user, refNumbers, audit);
        var kyc = new KycService(db.Context, user, audit, registration, notifications);
        var applications = new ShareholderApplicationService(db.Context, user, refNumbers, audit, notifications);
        var application = await applications.CreateDraftAsync(new CreateApplicationRequest(ApplicantType.Personal, group.Id));
        application.NameEn = "Daw Hla Hla";
        application.DateOfBirth = new DateOnly(1985, 1, 1);
        application.NrcNumber = "12/YAKANA(N)000111";
        application.Mobile = "09-111222333";
        await applications.SaveDraftAsync(application);
        await applications.SubmitAsync(application.Id);
        var kycCase = db.Context.KycCases.Single();
        await kyc.DecideAsync(new KycDecisionRequest(kycCase.Id, KycResult.Approved, RiskRating.Low, "Clean", null));

        var result = await Service(db).RunAsync("RPT-013", new ReportParameters());

        var row = Assert.Single(result.Rows);
        Assert.Equal("Approved", row["Result"]);
        Assert.Equal("maker", row["Officer"]);
    }

    [Fact]
    public async Task RunAsync_AuditTrail_ListsLoggedEntriesNewestFirst()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-014");
        var user = new FakeCurrentUserService { UserId = "u1", UserName = "maker@ems.local" };
        var audit = new AuditService(db.Context, user);
        await audit.LogAsync("Create", "SA", "ShareholderApplication", "SA-2026-000001");
        await audit.LogAsync("Submit", "SA", "ShareholderApplication", "SA-2026-000001");

        var result = await Service(db).RunAsync("RPT-014", new ReportParameters());

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("Submit", result.Rows[0]["Action"]); // most recent first
    }

    [Fact]
    public async Task RunAsync_ReconciliationExceptions_ListsOpenControlBreaks()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-015");
        db.Context.ReconciliationResults.Add(new EMS.Domain.Reporting.ReconciliationResult
        {
            ControlName = "NegativeShareBalance:Shareholder1:Class1",
            BusinessDate = new DateOnly(2026, 6, 30),
            ExpectedValue = 0,
            ActualValue = -50,
            Difference = -50,
            Status = "Open",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system"
        });
        db.Context.SaveChanges();

        var result = await Service(db).RunAsync("RPT-015", new ReportParameters());

        var row = Assert.Single(result.Rows);
        Assert.Equal(-50m, row["Difference"]);
        Assert.Equal("Open", row["Status"]);
    }

    [Fact]
    public async Task RunAsync_UserAccessReview_ListsUsersOrderedByUserName()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-016");
        db.Context.Users.Add(new ApplicationUser { UserName = "zed@ems.local", Email = "zed@ems.local", FullName = "Zed User", Status = UserStatus.Active });
        db.Context.Users.Add(new ApplicationUser { UserName = "amy@ems.local", Email = "amy@ems.local", FullName = "Amy User", Status = UserStatus.Active });
        db.Context.SaveChanges();

        var result = await Service(db).RunAsync("RPT-016", new ReportParameters());

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("amy@ems.local", result.Rows[0]["UserName"]); // alphabetical
        Assert.Equal("Never", result.Rows[0]["LastLogin"]);
    }
}
