using EMS.Infrastructure.Services;
using EMS.Tests.TestSupport;
using Xunit;

namespace EMS.Tests.Services;

public class ReferenceNumberServiceTests
{
    [Fact]
    public async Task NextAsync_FirstCallForModuleAndYear_StartsAtOne()
    {
        using var db = new SqliteTestDb();
        var service = new ReferenceNumberService(db.Context);

        var result = await service.NextAsync("IS", 2026);

        Assert.Equal("IS-2026-000001", result);
    }

    [Fact]
    public async Task NextAsync_SecondCallSameModuleAndYear_Increments()
    {
        using var db = new SqliteTestDb();
        var service = new ReferenceNumberService(db.Context);

        await service.NextAsync("IS", 2026);
        var second = await service.NextAsync("IS", 2026);

        Assert.Equal("IS-2026-000002", second);
    }

    [Fact]
    public async Task NextAsync_DifferentModules_DoNotShareASequence()
    {
        using var db = new SqliteTestDb();
        var service = new ReferenceNumberService(db.Context);

        var issue = await service.NextAsync("IS", 2026);
        var transfer = await service.NextAsync("TS", 2026);

        Assert.Equal("IS-2026-000001", issue);
        Assert.Equal("TS-2026-000001", transfer);
    }

    [Fact]
    public async Task NextAsync_DifferentYears_RestartTheSequence()
    {
        using var db = new SqliteTestDb();
        var service = new ReferenceNumberService(db.Context);

        await service.NextAsync("IS", 2025);
        var nextYear = await service.NextAsync("IS", 2026);

        Assert.Equal("IS-2026-000001", nextYear);
    }

    [Fact]
    public async Task NextShareholderNoAsync_UsesTwoDigitYearAndSevenDigitSequence()
    {
        using var db = new SqliteTestDb();
        var service = new ReferenceNumberService(db.Context);

        var result = await service.NextShareholderNoAsync();

        Assert.StartsWith("SH-", result);
        Assert.Equal(12, result.Length); // "SH-" + 2-digit year + 7-digit sequence = 3 + 2 + 7
    }

    [Fact]
    public async Task NextCertificateNoAsync_UsesTheCertModule()
    {
        using var db = new SqliteTestDb();
        var service = new ReferenceNumberService(db.Context);

        var result = await service.NextCertificateNoAsync();

        Assert.StartsWith("CERT-", result);
    }
}
