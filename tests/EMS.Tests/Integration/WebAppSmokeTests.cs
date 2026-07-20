using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace EMS.Tests.Integration;

/// <summary>Boots the real ASP.NET Core pipeline (routing, Identity, DbSeeder) end to end over a throwaway
/// SQLite file per test class. Runs in the "Testing" environment so DbSeeder skips its ~20,000-row demo
/// register (see Program.cs / DbSeeder.SeedAsync(seedDemoData:)) and only seeds roles, the admin user, and
/// master data - fast enough to run on every build.
///
/// Program.cs reads Database:Provider from configuration *before* WebApplicationBuilder.Build() runs, which
/// is earlier than WebApplicationFactory's ConfigureAppConfiguration/ConfigureWebHost hooks take effect. Since
/// the host runs in-process, plain process environment variables (picked up by the default
/// AddEnvironmentVariables() source during WebApplication.CreateBuilder itself) are the one override that is
/// guaranteed to be visible at that point.</summary>
public class WebAppSmokeTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ems-smoke-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public WebAppSmokeTests()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("Database__Provider", "Sqlite");
        Environment.SetEnvironmentVariable("ConnectionStrings__Sqlite", $"Data Source={_dbPath}");

        _factory = new WebApplicationFactory<Program>();
    }

    [Fact]
    public async Task AnonymousRequestToTheDashboard_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task LoginPage_LoadsSuccessfullyForAnAnonymousUser()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Account/Login");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Login_WithTheSeededAdminCredentials_SucceedsAndReachesTheDashboard()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });

        var loginPage = await client.GetAsync("/Account/Login");
        var loginHtml = await loginPage.Content.ReadAsStringAsync();
        var token = ExtractAntiForgeryToken(loginHtml);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserName"] = "admin@ems.local",
            ["Password"] = "Passw0rd!123",
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/Login", form);

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Invalid username or password", html);
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
