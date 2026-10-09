using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace EMS.Tests.Integration;

/// <summary>Boots the real ASP.NET Core pipeline (routing, Identity, DbSeeder) end to end over a throwaway
/// SQLite file per test class. Runs in the "Testing" environment so DbSeeder skips its demo shareholders
/// and sample activity (see Program.cs / DbSeeder.SeedAsync(seedDemoData:)) and only seeds roles, the admin
/// user, and master data - fast enough to run on every build.</summary>
public class WebAppSmokeTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ems-smoke-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public WebAppSmokeTests()
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
    }
}
