using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EMS.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so `dotnet ef migrations add/update` can build EmsDbContext without running the
/// full Web host (which performs migration + seeding at startup). SQL Server is the spec's target provider,
/// so migrations are generated against it; the connection string here is only used by design-time tooling.
/// </summary>
public class EmsDbContextFactory : IDesignTimeDbContextFactory<EmsDbContext>
{
    public EmsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EmsDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=EMS;Trusted_Connection=True;TrustServerCertificate=True;");
        return new EmsDbContext(optionsBuilder.Options);
    }
}
