using EMS.Application.Shares;
using EMS.Infrastructure.Persistence;
using EMS.Infrastructure.Services;
using EMS.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EMS.Tests.Services;

/// <summary>16.1/13.1.1 - PostAsync chains each new entry's running balance off the previous row
/// (ShareLedgerService.cs: "read the last entry, then add the delta") rather than summing deltas. That
/// read-then-write has no explicit locking, so this test drives concurrent postings for the same
/// shareholder/share-class pair over separate connections to the same on-disk database - the only way to
/// observe genuine cross-connection interleaving, since a single SqliteTestDb/EmsDbContext is not thread-safe
/// and would not exercise the race at all.</summary>
public class ShareLedgerConcurrencyTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ems-ledger-concurrency-{Guid.NewGuid():N}.db");

    private EmsDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EmsDbContext>().UseSqlite($"Data Source={_dbPath}").Options;
        return new EmsDbContext(options);
    }

    [Fact]
    public async Task PostAsync_ConcurrentPostingsForTheSameShareholder_NeverProduceTwoEntriesWithTheSameRunningBalance()
    {
        long shareholderId, shareClassId;
        using (var setup = NewContext())
        {
            await setup.Database.EnsureCreatedAsync();
            var group = TestSeed.Group(setup);
            var shareClass = TestSeed.ShareClass(setup);
            var shareholder = TestSeed.ActiveShareholder(setup, group.Id, "SH-24000001");
            shareholderId = shareholder.Id;
            shareClassId = shareClass.Id;
        }

        const int concurrentPostings = 10;
        var tasks = Enumerable.Range(0, concurrentPostings).Select(async i =>
        {
            using var db = NewContext();
            var service = new ShareLedgerService(db, new FakeCurrentUserService { UserId = $"maker-{i}" });
            try
            {
                await service.PostAsync(new PostLedgerEntryRequest(shareholderId, shareClassId, 1m, 10_000m, 0m, $"CONCURRENT-{i}", null, null, null, new DateOnly(2026, 1, 1)));
                return true;
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
                // SQLite allows only one writer at a time; a losing connection surfaces "database is locked"
                // rather than silently corrupting a row, since EF Core's default SQLite provider sets no
                // busy_timeout. A failed posting is an acceptable outcome here - a *duplicated* running
                // balance on two successful postings would not be.
                return false;
            }
        });

        var results = await Task.WhenAll(tasks);

        using var verify = NewContext();
        var entries = await verify.ShareLedgerEntries
            .Where(l => l.ShareholderId == shareholderId && l.ShareClassId == shareClassId)
            .ToListAsync();

        Assert.Equal(results.Count(r => r), entries.Count);
        var distinctBalances = entries.Select(e => e.RunningQuantityBalance).Distinct().Count();
        Assert.Equal(entries.Count, distinctBalances); // every successfully posted entry has a unique running balance
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
