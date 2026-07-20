using EMS.Domain.Common;
using EMS.Domain.MasterData;
using EMS.Domain.Shareholders;
using EMS.Infrastructure.Seed;
using EMS.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EMS.Tests.Services;

/// <summary>Startup seeding: idempotent core seed (roles/admin/master data), the 3-row demo register, and the
/// ~20,000-row bulk register. SeedDemoTransactionsAsync/SeedBulkRegisterAsync are internal specifically so
/// these tests can call them directly rather than only through the IServiceProvider-based SeedAsync.</summary>
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
        Assert.Empty(host.Context.Shareholders); // seedDemoData:false skips both demo and bulk seeding
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
    public async Task SeedAsync_WithDemoData_AlsoSeedsTheThreeRowDemoRegister()
    {
        using var host = new IdentityTestHost();

        await DbSeeder.SeedAsync(host.Services, seedDemoData: true);

        // seedDemoData:true runs SeedDemoTransactionsAsync but SeedBulkRegisterAsync's own guard
        // (Shareholders.Count < 20000) still lets the ~20,000-row bulk pass run after it - too slow to pay
        // for in every DbSeeder test, so this only asserts on the fast demo-register part.
        Assert.True(await host.Context.Shareholders.CountAsync() >= 3);
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
    public async Task SeedDemoTransactionsAsync_CreatesExactlyThreeActiveShareholdersWithMatchingLedgerAndCertificates()
    {
        using var db = new SqliteTestDb();
        SeedMasterData(db.Context);

        await DbSeeder.SeedDemoTransactionsAsync(db.Context);

        var shareholders = db.Context.Shareholders.ToList();
        Assert.Equal(3, shareholders.Count);
        Assert.All(shareholders, s => Assert.Equal(ShareholderStatus.Active, s.Status));
        Assert.Equal(3, db.Context.ShareLedgerEntries.Count());
        Assert.Equal(3, db.Context.ShareCertificates.Count());

        var daw = shareholders.Single(s => s.ShareholderNo == "SH-250000001");
        var ledgerEntry = db.Context.ShareLedgerEntries.Single(l => l.ShareholderId == daw.Id);
        Assert.Equal(12500m, ledgerEntry.RunningQuantityBalance);
        Assert.Equal(125_000_000m, ledgerEntry.RunningPaidUpCapital);
    }

    [Fact]
    public async Task SeedDemoTransactionsAsync_CalledTwice_IsANoOpTheSecondTime()
    {
        using var db = new SqliteTestDb();
        SeedMasterData(db.Context);

        await DbSeeder.SeedDemoTransactionsAsync(db.Context);
        await DbSeeder.SeedDemoTransactionsAsync(db.Context);

        Assert.Equal(3, db.Context.Shareholders.Count());
    }

    [Fact]
    public async Task SeedBulkRegisterAsync_RegisterAlreadyAtTargetSize_ReturnsWithoutAddingMoreRows()
    {
        using var db = new SqliteTestDb();
        var (groupId, _) = SeedMasterData(db.Context);

        // Cheaply reach the 20,000-row guard threshold without paying for the full generator (certs, ledger
        // postings, transfers, applications) - this test is about the idempotency guard, not the data itself.
        const int alreadySeeded = 20000;
        var batch = new List<Shareholder>(500);
        for (var i = 0; i < alreadySeeded; i++)
        {
            batch.Add(new Shareholder
            {
                ShareholderNo = $"SH-PRESEED-{i:D7}",
                Type = ApplicantType.Personal,
                ShareholderGroupId = groupId,
                Status = ShareholderStatus.Active,
                RegistrationDate = new DateOnly(2020, 1, 1),
                KycStatus = KycResult.Approved,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "system"
            });
            if (batch.Count == 500)
            {
                db.Context.Shareholders.AddRange(batch);
                db.Context.SaveChanges();
                batch.Clear();
            }
        }

        await DbSeeder.SeedBulkRegisterAsync(db.Context);

        Assert.Equal(alreadySeeded, db.Context.Shareholders.Count());
    }

    /// <summary>The only test that runs the real ~20,000-row generator end to end (certificates, opening
    /// ledger postings, transfers, and the application pipeline) - deliberately slow (~45s on SQLite) because
    /// it is otherwise never exercised by anything in this suite. Confirms the generator both produces a
    /// consistent bank-scale register and is idempotent on a second call.</summary>
    [Fact(Timeout = 120_000)]
    public async Task SeedBulkRegisterAsync_FullRun_ProducesTwentyThousandShareholdersAndIsIdempotentOnRerun()
    {
        using var db = new SqliteTestDb();
        TestSeed.Group(db.Context, "PERSONAL");
        TestSeed.Group(db.Context, "PUBLIC_COMPANY");
        TestSeed.Group(db.Context, "COOPERATIVE");
        TestSeed.Group(db.Context, "STAFF");
        TestSeed.ShareClass(db.Context);
        db.Context.NrcPrefixes.Add(new NrcPrefix { Code = "12/YAKANA", NameEn = "Yangon", NameMm = "Yangon", StateRegion = "Yangon", TownshipCode = "YAKANA", CitizenshipType = "Citizen", EffectiveFrom = new DateOnly(2020, 1, 1) });
        db.Context.SaveChanges();

        await DbSeeder.SeedBulkRegisterAsync(db.Context);

        var totalShareholders = db.Context.Shareholders.Count();
        Assert.True(totalShareholders >= 20000, $"Expected at least 20,000 shareholders, found {totalShareholders}.");
        Assert.True(db.Context.ShareCertificates.Any());
        Assert.True(db.Context.ShareLedgerEntries.Any());
        Assert.True(db.Context.ShareTransactions.Any(t => t.Type == EMS.Domain.Common.ShareTransactionType.TransferShares));
        Assert.True(db.Context.ShareholderApplications.Any());

        await DbSeeder.SeedBulkRegisterAsync(db.Context); // guarded - must not add a second cohort

        Assert.Equal(totalShareholders, db.Context.Shareholders.Count());
    }
}
