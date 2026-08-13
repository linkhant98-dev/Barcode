using EMS.Domain.Common;
using EMS.Domain.MasterData;
using EMS.Domain.Shareholders;
using EMS.Infrastructure.Seed;
using EMS.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EMS.Tests.Services;

/// <summary>Startup seeding: idempotent core seed (roles/admin/master data), the 5-row demo register, and 5
/// rows each of Issue Shares, Transfer Shares, and pipeline Applications. SeedDemoTransactionsAsync/
/// SeedSampleActivityAsync are internal specifically so these tests can call them directly rather than only
/// through the IServiceProvider-based SeedAsync.</summary>
public class DbSeederTests
{
    [Fact]
    public async Task SeedAsync_WithoutDemoData_SeedsRolesAdminAndMasterDataButLeavesShareholdersEmpty()
    {
        using var host = new IdentityTestHost();

        await DbSeeder.SeedAsync(host.Services, seedDemoData: false);

        Assert.Equal(DbSeeder.Roles.Length, await host.Context.Roles.CountAsync());
        var admin = await host.Users.FindByEmailAsync("admin@ems.local");
        Assert.NotNull(admin);
        Assert.True(await host.Users.IsInRoleAsync(admin, "System Administrator"));
        Assert.True(await host.Context.ShareholderGroups.AnyAsync());
        Assert.True(await host.Context.ReportDefinitions.CountAsync() == 16);
        Assert.Empty(host.Context.Shareholders); // seedDemoData:false skips both demo seeds
    }

    [Fact]
    public async Task SeedAsync_CalledTwice_DoesNotDuplicateRolesOrTheAdminUser()
    {
        using var host = new IdentityTestHost();

        await DbSeeder.SeedAsync(host.Services, seedDemoData: false);
        await DbSeeder.SeedAsync(host.Services, seedDemoData: false);

        Assert.Equal(DbSeeder.Roles.Length, await host.Context.Roles.CountAsync());
        Assert.Equal(1, await host.Context.Users.CountAsync(u => u.Email == "admin@ems.local"));
        Assert.Equal(16, await host.Context.ReportDefinitions.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_WithDemoData_SeedsFiveShareholdersAndFiveRowsOfSampleActivity()
    {
        using var host = new IdentityTestHost();

        await DbSeeder.SeedAsync(host.Services, seedDemoData: true);

        Assert.Equal(5, await host.Context.Shareholders.CountAsync());
        Assert.Equal(5, await host.Context.ShareCertificates.CountAsync());
        Assert.Equal(5, await host.Context.ShareholderApplications.CountAsync());
        Assert.Equal(5, await host.Context.ShareTransactions.CountAsync(t => t.Type == ShareTransactionType.IssueShares));
        Assert.Equal(5, await host.Context.ShareTransactions.CountAsync(t => t.Type == ShareTransactionType.TransferShares));
        Assert.Equal(2, await host.Context.DividendEvents.CountAsync());
    }

    private static void SeedAllGroups(EMS.Infrastructure.Persistence.EmsDbContext db)
    {
        TestSeed.Group(db, "BOD");
        TestSeed.Group(db, "STAFF");
        TestSeed.Group(db, "PERSONAL");
        TestSeed.Group(db, "PUBLIC_COMPANY");
        TestSeed.Group(db, "COOPERATIVE");
    }

    private static (long GroupId, long ShareClassId) SeedMasterData(EMS.Infrastructure.Persistence.EmsDbContext db)
    {
        var group = TestSeed.Group(db, "PERSONAL");
        var shareClass = TestSeed.ShareClass(db);
        db.NrcPrefixes.Add(new NrcPrefix { Code = "12/YAKANA", NameEn = "Yangon", NameMm = "Yangon", StateRegion = "Yangon", TownshipCode = "YAKANA", CitizenshipType = "Citizen", EffectiveFrom = new DateOnly(2020, 1, 1) });
        db.SaveChanges();
        return (group.Id, shareClass.Id);
    }

    [Fact]
    public async Task SeedDemoTransactionsAsync_CreatesExactlyFiveActiveShareholdersWithMatchingLedgerAndCertificates()
    {
        using var db = new SqliteTestDb();
        SeedAllGroups(db.Context);
        TestSeed.ShareClass(db.Context);
        db.Context.SaveChanges();

        await DbSeeder.SeedDemoTransactionsAsync(db.Context);

        var shareholders = db.Context.Shareholders.ToList();
        Assert.Equal(5, shareholders.Count);
        Assert.All(shareholders, s => Assert.Equal(ShareholderStatus.Active, s.Status));
        Assert.Equal(5, db.Context.ShareLedgerEntries.Count());
        Assert.Equal(5, db.Context.ShareCertificates.Count());

        var daw = shareholders.Single(s => s.ShareholderNo == "SH-250000001");
        var ledgerEntry = db.Context.ShareLedgerEntries.Single(l => l.ShareholderId == daw.Id);
        Assert.Equal(12500m, ledgerEntry.RunningQuantityBalance);
        Assert.Equal(125_000_000m, ledgerEntry.RunningPaidUpCapital);
    }

    [Fact]
    public async Task SeedDemoTransactionsAsync_CalledTwice_IsANoOpTheSecondTime()
    {
        using var db = new SqliteTestDb();
        SeedAllGroups(db.Context);
        TestSeed.ShareClass(db.Context);
        db.Context.SaveChanges();

        await DbSeeder.SeedDemoTransactionsAsync(db.Context);
        await DbSeeder.SeedDemoTransactionsAsync(db.Context);

        Assert.Equal(5, db.Context.Shareholders.Count());
    }

    [Fact]
    public async Task SeedSampleActivityAsync_RegisterAlreadyHasTransactions_ReturnsWithoutAddingMoreRows()
    {
        using var db = new SqliteTestDb();
        var (groupId, shareClassId) = SeedMasterData(db.Context);

        // A single pre-existing transaction is enough to trip the guard - this test is about the
        // idempotency guard, not the data itself.
        var shareholder = new Shareholder
        {
            ShareholderNo = "SH-PRESEED-0000001",
            Type = ApplicantType.Personal,
            ShareholderGroupId = groupId,
            Status = ShareholderStatus.Active,
            RegistrationDate = new DateOnly(2020, 1, 1),
            KycStatus = KycResult.Approved,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system"
        };
        db.Context.Shareholders.Add(shareholder);
        db.Context.SaveChanges();
        db.Context.ShareTransactions.Add(new EMS.Domain.Shares.ShareTransaction
        {
            TransactionNo = "IS-2026-0000001",
            Type = ShareTransactionType.IssueShares,
            Status = WorkflowStatus.Completed,
            EffectiveDate = new DateOnly(2026, 1, 1),
            MakerUserId = "system",
            TotalAmount = 100_000m
        });
        db.Context.SaveChanges();

        await DbSeeder.SeedSampleActivityAsync(db.Context);

        Assert.Equal(1, db.Context.ShareTransactions.Count());
        Assert.Empty(db.Context.ShareholderApplications);
    }

    /// <summary>Runs the sample-activity generator end to end against the five demo shareholders (issue
    /// top-ups, transfers, the application pipeline, and dividend events). Confirms it produces the expected
    /// dataset and is idempotent on a second call.</summary>
    [Fact]
    public async Task SeedSampleActivityAsync_FullRun_ProducesFiveRowsPerTypeAndIsIdempotentOnRerun()
    {
        using var db = new SqliteTestDb();
        SeedAllGroups(db.Context);
        TestSeed.ShareClass(db.Context);
        db.Context.NrcPrefixes.Add(new NrcPrefix { Code = "12/YAKANA", NameEn = "Yangon", NameMm = "Yangon", StateRegion = "Yangon", TownshipCode = "YAKANA", CitizenshipType = "Citizen", EffectiveFrom = new DateOnly(2020, 1, 1) });
        db.Context.SaveChanges();
        await DbSeeder.SeedDemoTransactionsAsync(db.Context);

        await DbSeeder.SeedSampleActivityAsync(db.Context);

        Assert.Equal(5, db.Context.ShareTransactions.Count(t => t.Type == ShareTransactionType.IssueShares));
        Assert.Equal(5, db.Context.ShareTransactions.Count(t => t.Type == ShareTransactionType.TransferShares));
        Assert.Equal(5, db.Context.ShareholderApplications.Count());
        Assert.Equal(2, db.Context.DividendEvents.Count());
        Assert.Equal(10, db.Context.DividendEntitlements.Count()); // 2 events x 5 demo shareholders

        var totalTransactions = db.Context.ShareTransactions.Count();
        var totalDividendEvents = db.Context.DividendEvents.Count();
        await DbSeeder.SeedSampleActivityAsync(db.Context); // guarded - must not add a second cohort

        Assert.Equal(totalTransactions, db.Context.ShareTransactions.Count());
        Assert.Equal(totalDividendEvents, db.Context.DividendEvents.Count());
    }
}
