using EMS.Infrastructure.Identity;
using EMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EMS.Tests.TestSupport;

/// <summary>A real Identity stack (UserManager/RoleManager) over a SQLite in-memory database, for tests that
/// genuinely need ASP.NET Core Identity rather than a fake - NotificationService.NotifyRoleAsync, and
/// DbSeeder.SeedAsync (which resolves RoleManager/UserManager straight off IServiceProvider).</summary>
public sealed class IdentityTestHost : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public EmsDbContext Context { get; }
    public UserManager<ApplicationUser> Users { get; }
    public RoleManager<ApplicationRole> Roles { get; }
    public IServiceProvider Services => _scope.ServiceProvider;

    public IdentityTestHost()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<EmsDbContext>(options => options.UseSqlite(_connection));
        services.AddIdentity<ApplicationUser, ApplicationRole>()
            .AddEntityFrameworkStores<EmsDbContext>()
            .AddDefaultTokenProviders();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();

        Context = _scope.ServiceProvider.GetRequiredService<EmsDbContext>();
        Context.Database.EnsureCreated();
        Users = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Roles = _scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    }

    public async Task<ApplicationUser> CreateUserAsync(string email)
    {
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = email };
        var result = await Users.CreateAsync(user, "Passw0rd!123");
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        return user;
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _connection.Dispose();
    }
}
