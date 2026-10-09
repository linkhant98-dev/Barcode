using System.Globalization;
using EMS.Application.Abstractions;
using EMS.Infrastructure;
using EMS.Infrastructure.Identity;
using EMS.Infrastructure.Jobs;
using EMS.Infrastructure.Persistence;
using EMS.Infrastructure.Seed;
using EMS.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// 3.2 / 16 - SQL Server is the target provider. Local/sandbox development can fall back to SQLite via
// Database:Provider in appsettings.Development.json so the app runs without a live SQL Server instance.
var dbProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";
builder.Services.AddDbContext<EmsDbContext>(options =>
{
    if (dbProvider == "Sqlite")
        options.UseSqlite(builder.Configuration.GetConnectionString("Sqlite"));
    else
        options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"));
});

// 12.1 / 12.3 - password policy, lockout, MFA-ready Identity store.
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequiredLength = 10;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireDigit = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.SignIn.RequireConfirmedAccount = false;
})
    .AddEntityFrameworkStores<EmsDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    // 20 - session timeout with warning handled client-side (see _SessionTimeout partial).
    options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
    options.SlidingExpiration = true;
});

// 17 - bilingual English / Myanmar UI.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("my") };
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders =
    [
        new CookieRequestCultureProvider { CookieName = CookieRequestCultureProvider.DefaultCookieName }
    ];
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddEmsInfrastructureServices();

// 21.1 - background jobs (approval reminders, daily reconciliation). See each job's XML doc for the
// demo-scale interval used here vs. the spec's hourly/daily targets.
builder.Services.AddHostedService<ApprovalReminderJob>();
builder.Services.AddHostedService<ReconciliationJob>();

builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EmsDbContext>();
    if (dbProvider == "Sqlite")
        await db.Database.EnsureCreatedAsync();
    else
        await db.Database.MigrateAsync();

    await DbSeeder.SeedAsync(scope.ServiceProvider, seedDemoData: !app.Environment.IsEnvironment("Testing"));
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

var localizationOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(localizationOptions);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();

/// <summary>Exposes the top-level-statements Program class to EMS.Tests' WebApplicationFactory&lt;Program&gt;.</summary>
public partial class Program { }
