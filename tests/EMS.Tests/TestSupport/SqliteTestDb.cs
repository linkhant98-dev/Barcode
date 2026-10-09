using EMS.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EMS.Tests.TestSupport;

/// <summary>
/// A real EmsDbContext backed by an in-memory SQLite database (not the EF Core InMemory provider, which
/// silently skips constraints/translations that matter here - see QueryExtensions.SumDecimalAsync). The
/// connection is kept open for the lifetime of the instance because closing it drops an in-memory SQLite
/// database entirely.
/// </summary>
public sealed class SqliteTestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    public EmsDbContext Context { get; }

    public SqliteTestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<EmsDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new EmsDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
