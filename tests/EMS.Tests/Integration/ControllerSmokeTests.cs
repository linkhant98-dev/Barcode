using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace EMS.Tests.Integration;

/// <summary>A GET-only sweep over every remaining controller's main screen, authenticated as the seeded admin
/// (who holds every role, including the System-Administrator-only ones). None of these controllers had any
/// HTTP-level coverage before - each renders its full Razor view against a near-empty (roles/admin/master-data
/// only) database, so this catches wiring bugs (missing DI registrations, view-model mismatches, null-ref on
/// empty collections) that service-layer unit tests can't reach.</summary>
public class ControllerSmokeTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ems-smoke-sweep-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public ControllerSmokeTests()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("Database__Provider", "Sqlite");
        Environment.SetEnvironmentVariable("ConnectionStrings__Sqlite", $"Data Source={_dbPath}");
        _factory = new WebApplicationFactory<Program>();
    }

    [Theory]
    [InlineData("/ApprovalMatrix")] // [Authorize(Roles = "System Administrator")]
    [InlineData("/Approvals")]
    [InlineData("/Approvals/SubmittedByMe")]
    [InlineData("/Bonus")]
    [InlineData("/Delegations")]
    [InlineData("/Dividends")]
    [InlineData("/IssueShares")]
    [InlineData("/Kyc")]
    [InlineData("/MasterData")] // [Authorize(Roles = "System Administrator")]
    [InlineData("/Notifications")]
    [InlineData("/Permissions")] // [Authorize(Roles = "System Administrator")]
    [InlineData("/TransferShares")]
    [InlineData("/Users")] // [Authorize(Roles = "System Administrator")]
    [InlineData("/ShareholderApplications")]
    [InlineData("/Home/Error")]
    public async Task AuthenticatedAdmin_LoadsEveryRemainingControllersMainScreen(string path)
    {
        var client = await CreateAuthenticatedClientAsync("admin@ems.local");

        var response = await client.GetAsync(path);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ExportExcel_ProducesAValidXlsxDocument()
    {
        var client = await CreateAuthenticatedClientAsync("admin@ems.local");

        var response = await client.GetAsync("/Reports/ExportExcel?code=RPT-001");

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        // An .xlsx is a zip archive - "PK" magic bytes confirm ClosedXML actually produced one after the
        // 0.104.1 -> 0.105.0 upgrade (part of clearing the System.IO.Packaging advisory).
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        var client = _factory.CreateClient();
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

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("Database__Provider", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__Sqlite", null);
    }
}
