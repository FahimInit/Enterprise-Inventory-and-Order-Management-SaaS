using Inventory.Application.Common;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Inventory.API.IntegrationTests;

public sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private SqliteConnection? _sqliteKeepAliveConnection;
    private string? _postgresConnectionString;
    private string? _sqliteConnectionString;
    private string? _sqliteDbFilePath;

    public bool IsUsingRealPostgreSql { get; private set; }

    public async Task InitializeAsync()
    {
        var externalPgConnection = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION");
        if (!string.IsNullOrWhiteSpace(externalPgConnection))
        {
            _postgresConnectionString = externalPgConnection;
            IsUsingRealPostgreSql = true;
        }
        else
        {
            try
            {
                _postgresContainer = new PostgreSqlBuilder()
                    .WithImage("postgres:17-alpine")
                    .WithDatabase("enterprise_inventory_integration_tests")
                    .WithUsername("postgres_test")
                    .WithPassword("postgres_test_pwd")
                    .Build();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await _postgresContainer.StartAsync(cts.Token);
                _postgresConnectionString = _postgresContainer.GetConnectionString();
                IsUsingRealPostgreSql = true;
            }
            catch
            {
                // Local Docker daemon is not running; use a dedicated relational SQLite database with WAL mode
                // so each concurrent HTTP request scope opens its own thread-safe connection.
                IsUsingRealPostgreSql = false;
                _sqliteDbFilePath = Path.Combine(Path.GetTempPath(), $"inventory_integration_{Guid.NewGuid():N}.db");
                _sqliteConnectionString = $"Data Source={_sqliteDbFilePath};Default Timeout=30;Pooling=True";
                _sqliteKeepAliveConnection = new SqliteConnection(_sqliteConnectionString);
                await _sqliteKeepAliveConnection.OpenAsync();

                await using var walCmd = _sqliteKeepAliveConnection.CreateCommand();
                walCmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=30000;";
                await walCmd.ExecuteNonQueryAsync();
            }
        }

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (IsUsingRealPostgreSql)
        {
            await dbContext.Database.MigrateAsync();
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Testing:OverrideDbContext", "true");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();

            services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                if (IsUsingRealPostgreSql && !string.IsNullOrWhiteSpace(_postgresConnectionString))
                {
                    options.UseNpgsql(_postgresConnectionString, npgsql =>
                    {
                        npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    });
                    options.UseSnakeCaseNamingConvention();
                }
                else if (!string.IsNullOrWhiteSpace(_sqliteConnectionString))
                {
                    options.UseSqlite(_sqliteConnectionString);
                    options.UseSnakeCaseNamingConvention();
                }

                options.AddInterceptors(sp.GetRequiredService<TenantWriteEnforcementInterceptor>());
            });

            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        });
    }

    public new async Task DisposeAsync()
    {
        if (_postgresContainer is not null)
        {
            await _postgresContainer.DisposeAsync();
        }

        if (_sqliteKeepAliveConnection is not null)
        {
            await _sqliteKeepAliveConnection.DisposeAsync();
        }

        SqliteConnection.ClearAllPools();

        if (!string.IsNullOrWhiteSpace(_sqliteDbFilePath) && File.Exists(_sqliteDbFilePath))
        {
            try
            {
                File.Delete(_sqliteDbFilePath);
            }
            catch
            {
                // Ignore temp file cleanup race
            }
        }

        await base.DisposeAsync();
    }
}
