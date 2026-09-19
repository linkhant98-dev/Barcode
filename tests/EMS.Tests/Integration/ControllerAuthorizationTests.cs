using System.Net;
using System.Text.RegularExpressions;
using EMS.Domain.Common;
using EMS.Domain.Shares;
using EMS.Infrastructure.Identity;
using EMS.Infrastructure.Persistence;
using EMS.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EMS.Tests.Integration;

/// <summary>Section 12.2/12.3 - controllers are gated by [Authorize] (any authenticated user) or
/// [Authorize(Roles = ...)] (a specific role); this exercises that gate over HTTP rather than by inspecting
/// attributes, plus the CSV/PDF export actions that are otherwise only reachable through the browser.</summary>
public class ControllerAuthorizationTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ems-authz-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public ControllerAuthorizationTests()
    {
        // Configuration is set per-factory (UseSetting/UseEnvironment), never via Environment.SetEnvironmentVariable:
        // that mutates process-wide state, and WebApplicationFactory only reads it lazily when the host actually
        // starts (on the first CreateClient() call) - not at construction time - so a test class running
        // concurrently with this one (xUnit parallelizes across classes by default) can overwrite it first,
        // pointing two factories at the same SQLite file and racing on EnsureCreatedAsync ("table already exists").
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Testing")
            .UseSetting("Database:Provider", "Sqlite")
            .UseSetting("ConnectionStrings:Sqlite", $"Data Source={_dbPath}"));
    }

    [Theory]
    [InlineData("/Shareholders")]
    [InlineData("/Certificates")]
    [InlineData("/Reports")]
    [InlineData("/Audit")]
    public async Task AnonymousRequest_ToAProtectedController_RedirectsToLogin(string path)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("/Shareholders")]
    [InlineData("/Certificates")]
    [InlineData("/Reports")]
    public async Task AuthenticatedAdmin_CanAccessGeneralControllers(string path)
    {
        var client = await CreateAuthenticatedClientAsync("admin@ems.local");

        var response = await client.GetAsync(path);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AuthenticatedAdmin_HoldingSystemAdministratorRole_CanAccessTheRoleRestrictedAuditController()
    {
        var client = await CreateAuthenticatedClientAsync("admin@ems.local");

        var response = await client.GetAsync("/Audit");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AuthenticatedUser_WithoutTheRequiredRole_IsRedirectedToAccessDeniedRatherThanTheAuditLog()
    {
        await CreateUserAsync("plainuser@ems.local"); // no role assignment
        var client = await CreateAuthenticatedClientAsync("plainuser@ems.local", allowAutoRedirect: false);

        var response = await client.GetAsync("/Audit");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task ExportCsv_ReturnsACsvFileWithTheCsvContentType()
    {
        var client = await CreateAuthenticatedClientAsync("admin@ems.local");

        var response = await client.GetAsync("/Shareholders/ExportCsv");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task DownloadPdf_ForAnExistingCertificate_ReturnsAPdfDocument()
    {
        long certificateId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmsDbContext>();
            var group = TestSeed.Group(db, "AUTHZ_GROUP");
            var shareClass = TestSeed.ShareClass(db, "AUTHZ_ORD");
            var shareholder = TestSeed.ActiveShareholder(db, group.Id, "SH-90000001");
            var certificate = new ShareCertificate
            {
                CertificateNumber = "CERT-TEST-0001",
                ShareholderId = shareholder.Id,
                ShareClassId = shareClass.Id,
                Quantity = 100m,
                Status = CertificateStatus.Printed,
                IssueDate = new DateOnly(2026, 1, 1),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "system"
            };
            db.ShareCertificates.Add(certificate);
            db.SaveChanges();
            certificateId = certificate.Id;
        }

        var client = await CreateAuthenticatedClientAsync("admin@ems.local");
        var response = await client.GetAsync($"/Certificates/DownloadPdf/{certificateId}");

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 100, "Expected a non-trivial PDF byte stream.");
    }

    [Fact]
    public async Task DownloadPdf_ForAMissingCertificate_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync("admin@ems.local");

        var response = await client.GetAsync("/Certificates/DownloadPdf/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task CreateUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = email, Status = UserStatus.Active };
        var result = await userManager.CreateAsync(user, "Passw0rd!123");
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, bool allowAutoRedirect = true)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = allowAutoRedirect });
        var loginPage = await client.GetAsync("/Account/Login");
        var token = ExtractAntiForgeryToken(await loginPage.Content.ReadAsStringAsync());

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserName"] = email,
            ["Password"] = "Passw0rd!123",
            ["__RequestVerificationToken"] = token
        });

        await client.PostAsync("/Account/Login", form);
        return client;
    }

    private static readonly Regex AntiForgeryTokenPattern = new(
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"|value=\"([^\"]+)\"[^>]*name=\"__RequestVerificationToken\"",
        RegexOptions.Compiled);

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = AntiForgeryTokenPattern.Match(html);
        Assert.True(match.Success, "Anti-forgery token not found in the login page markup.");
        return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
    }

    public void Dispose()
    {
        _factory.Dispose();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
