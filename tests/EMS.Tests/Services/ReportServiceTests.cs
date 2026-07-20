using EMS.Application.Reporting;
using EMS.Application.Shares;
using EMS.Domain.Reporting;
using EMS.Domain.Security;
using EMS.Infrastructure.Services;
using EMS.Tests.TestSupport;
using Xunit;

namespace EMS.Tests.Services;

/// <summary>Section 13.7/13.8/13.9.5 - report row shape, group percentage math, and sensitive-data masking.</summary>
public class ReportServiceTests
{
    private static void SeedDefinition(SqliteTestDb db, string code, string name = "Report") =>
        db.Context.ReportDefinitions.Add(new ReportDefinition { ReportCode = code, NameEn = name, NameMm = name, Category = "Test" });

    [Fact]
    public async Task RunAsync_UnknownReportCode_Throws()
    {
        using var db = new SqliteTestDb();
        var service = new ReportService(db.Context, new FakeCurrentUserService(), new PermissionService(db.Context, new FakeCurrentUserService()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunAsync("RPT-999", new ReportParameters()));
    }

    [Fact]
    public async Task RunAsync_ShareholdersList_WithoutSensitivePermission_MasksTheNrcNumber()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-001", "Shareholders List");
        var group = TestSeed.Group(db.Context);
        var shareholder = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001");
        var user = new FakeCurrentUserService { RoleList = ["Report Viewer"] };
        var service = new ReportService(db.Context, user, new PermissionService(db.Context, user));

        var result = await service.RunAsync("RPT-001", new ReportParameters());

        var row = Assert.Single(result.Rows);
        var nrc = (string?)row["NrcOrReg"];
        Assert.NotNull(nrc);
        Assert.EndsWith("0000", nrc);
        Assert.DoesNotContain("YAKANA", nrc);
        Assert.Equal(shareholder.ShareholderNo, row["ShareholderNo"]);
    }

    [Fact]
    public async Task RunAsync_ShareholdersList_WithSensitivePermission_ShowsTheRawNrcNumber()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-001", "Shareholders List");
        db.Context.RolePermissions.Add(new RolePermission { RoleName = "Auditor", PermissionKey = "Reports.ViewSensitiveData" });
        db.Context.SaveChanges();
        var group = TestSeed.Group(db.Context);
        TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001");
        var user = new FakeCurrentUserService { RoleList = ["Auditor"] };
        var service = new ReportService(db.Context, user, new PermissionService(db.Context, user));

        var result = await service.RunAsync("RPT-001", new ReportParameters());

        var row = Assert.Single(result.Rows);
        Assert.Equal("12/YAKANA(N)000000", row["NrcOrReg"]);
    }

    [Fact]
    public async Task RunAsync_ShareholdersList_TotalSharesTotal_SumsAllRows()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-001", "Shareholders List");
        var group = TestSeed.Group(db.Context);
        var shareClass = TestSeed.ShareClass(db.Context);
        var a = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001", "Alice");
        var b = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000002", "Bob");
        var ledger = new ShareLedgerService(db.Context, new FakeCurrentUserService());
        await ledger.PostAsync(new PostLedgerEntryRequest(a.Id, shareClass.Id, 1000m, 10_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        await ledger.PostAsync(new PostLedgerEntryRequest(b.Id, shareClass.Id, 500m, 5_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        var user = new FakeCurrentUserService();
        var service = new ReportService(db.Context, user, new PermissionService(db.Context, user));

        var result = await service.RunAsync("RPT-001", new ReportParameters());

        Assert.Equal(1500m, result.Totals!["TotalShares"]);
    }

    [Fact]
    public async Task RunAsync_ShareholderGroupSummary_ComputesPercentageOfGrandTotal()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-002", "Shareholders Group Summary");
        var groupA = TestSeed.Group(db.Context, "PERSONAL");
        var groupB = TestSeed.Group(db.Context, "STAFF");
        var shareClass = TestSeed.ShareClass(db.Context);
        var a = TestSeed.ActiveShareholder(db.Context, groupA.Id, "SH-24000001");
        var b = TestSeed.ActiveShareholder(db.Context, groupB.Id, "SH-24000002");
        var ledger = new ShareLedgerService(db.Context, new FakeCurrentUserService());
        await ledger.PostAsync(new PostLedgerEntryRequest(a.Id, shareClass.Id, 7500m, 75_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        await ledger.PostAsync(new PostLedgerEntryRequest(b.Id, shareClass.Id, 2500m, 25_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        var user = new FakeCurrentUserService();
        var service = new ReportService(db.Context, user, new PermissionService(db.Context, user));

        var result = await service.RunAsync("RPT-002", new ReportParameters());

        Assert.Equal(2, result.Rows.Count);
        var personalRow = result.Rows.Single(r => Equals(r["Group"], "PERSONAL"));
        Assert.Equal(75.00m, personalRow["Percentage"]);
        var staffRow = result.Rows.Single(r => Equals(r["Group"], "STAFF"));
        Assert.Equal(25.00m, staffRow["Percentage"]);
        Assert.Equal(100m, result.Totals!["Percentage"]);
        Assert.Equal(10000m, result.Totals["TotalShares"]);
    }

    [Fact]
    public async Task RunAsync_ShareholderGroupSummary_EntriesAfterTheCutoffDateAreExcluded()
    {
        using var db = new SqliteTestDb();
        SeedDefinition(db, "RPT-002", "Shareholders Group Summary");
        var group = TestSeed.Group(db.Context);
        var shareClass = TestSeed.ShareClass(db.Context);
        var shareholder = TestSeed.ActiveShareholder(db.Context, group.Id, "SH-24000001");
        var ledger = new ShareLedgerService(db.Context, new FakeCurrentUserService());
        await ledger.PostAsync(new PostLedgerEntryRequest(shareholder.Id, shareClass.Id, 1000m, 10_000_000m, 0m, "OPENING", null, null, null, new DateOnly(2026, 1, 1)));
        await ledger.PostAsync(new PostLedgerEntryRequest(shareholder.Id, shareClass.Id, 500m, 5_000_000m, 0m, "TOP-UP", null, null, null, new DateOnly(2026, 6, 1)));
        var user = new FakeCurrentUserService();
        var service = new ReportService(db.Context, user, new PermissionService(db.Context, user));

        var result = await service.RunAsync("RPT-002", new ReportParameters(AsOfDate: new DateOnly(2026, 3, 1)));

        var row = Assert.Single(result.Rows);
        Assert.Equal(1000m, row["TotalShares"]);
    }
}
