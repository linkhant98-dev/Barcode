using System.Net;
using EMS.Domain.Common;
using EMS.Domain.Shares;
using EMS.Infrastructure.Persistence;
using EMS.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EMS.Tests.Integration;

/// <summary>
/// The public certificate verification page (QR target) must be reachable with no session, and must never leak
/// NRC number, address, or other shareholder PII - see <see cref="VerifyController"/> and
/// <see cref="EMS.Web.Models.VerifyCertificateViewModel"/>. This is the flip side of
/// <see cref="ControllerAuthorizationTests.AnonymousRequest_ToAProtectedController_RedirectsToLogin"/>: everywhere
/// else, anonymous means "redirect to login"; here it must mean "show the page".
/// </summary>
public class VerifyControllerTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ems-verify-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public VerifyControllerTests()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("Database__Provider", "Sqlite");
        Environment.SetEnvironmentVariable("ConnectionStrings__Sqlite", $"Data Source={_dbPath}");
        _factory = new WebApplicationFactory<Program>();
    }

    private long SeedCertificate(string certNo, CertificateStatus status, string holderName = "Daw Verify Test", string nrc = "12/YAKANA(N)000000")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmsDbContext>();
        var group = TestSeed.Group(db, $"GRP_{Guid.NewGuid():N}"[..12]);
        var shareClass = TestSeed.ShareClass(db, $"ORD_{Guid.NewGuid():N}"[..10]);
        var shareholder = TestSeed.ActiveShareholder(db, group.Id, $"SH-{Guid.NewGuid():N}"[..12], holderName);
        shareholder.Person!.NrcNumber = nrc;
        db.SaveChanges();

        var certificate = new ShareCertificate
        {
            CertificateNumber = certNo,
            ShareholderId = shareholder.Id,
            ShareClassId = shareClass.Id,
            Quantity = 8200m,
            Status = status,
            IssueDate = new DateOnly(2026, 1, 10),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system"
        };
        db.ShareCertificates.Add(certificate);
        db.SaveChanges();
        return certificate.Id;
    }

    [Fact]
    public async Task Details_ForAnExistingCertificate_IsReachableWithNoSessionAndReturnsSuccess()
    {
        SeedCertificate("CERT-VERIFY-0001", CertificateStatus.Active);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/verify/CERT-VERIFY-0001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Details_ForAnActiveCertificate_ShowsHolderNameShareClassAndQuantityButNeverNrcOrAddress()
    {
        SeedCertificate("CERT-VERIFY-0002", CertificateStatus.Active, "Daw Khin Public Test", "12/OuKaTha(N)998877");
        var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/verify/CERT-VERIFY-0002");

        Assert.Contains("CERT-VERIFY-0002", html);
        Assert.Contains("Daw Khin Public Test", html);
        Assert.Contains("8,200", html);
        Assert.Contains("Ordinary", html);
        Assert.Contains("verified", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OuKaTha", html);
        Assert.DoesNotContain("998877", html);
    }

    [Fact]
    public async Task Details_ForACancelledCertificate_ReportsItAsNoLongerValid()
    {
        SeedCertificate("CERT-VERIFY-0003", CertificateStatus.Cancelled);
        var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/verify/CERT-VERIFY-0003");

        Assert.Contains("cancelled", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Details_ForAnUnknownCertificateNumber_ReturnsAFriendlyNotFoundPageRatherThanAnError()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/verify/CERT-DOES-NOT-EXIST");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("not found", html, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _factory.Dispose();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("Database__Provider", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__Sqlite", null);
    }
}
