using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Trains.Persistence.IntegrationTests;

public sealed class PostgresTempDatabase : IAsyncDisposable {
    private const string EnabledEnvVar = "TRAINS_PG_TESTS";
    private const string AdminEnvVar = "TRAINS_PG_ADMIN";
    private const string BaseEnvVar = "TRAINS_PG_BASE";

    private PostgresTempDatabase(string adminConnectionString, string databaseName, string connectionString) {
        AdminConnectionString = adminConnectionString;
        DatabaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string AdminConnectionString { get; }
    public string DatabaseName { get; }
    public string ConnectionString { get; }

    public static async Task<PostgresTempDatabase> CreateAndMigrateAsync() {
        string? enabled = Environment.GetEnvironmentVariable(EnabledEnvVar);
        Skip.IfNot(string.Equals(enabled, "1", StringComparison.Ordinal), $"Postgres integration tests are disabled. Set env var {EnabledEnvVar}=1 to enable.");

        string adminCs = Environment.GetEnvironmentVariable(AdminEnvVar) ?? "Host=127.0.0.1;Database=postgres";
        string baseCs = Environment.GetEnvironmentVariable(BaseEnvVar) ?? "Host=127.0.0.1";

        var adminBuilder = new NpgsqlConnectionStringBuilder(adminCs);
        if (string.IsNullOrWhiteSpace(adminBuilder.Database))
            adminBuilder.Database = "postgres";

        var testBuilder = new NpgsqlConnectionStringBuilder(baseCs) {
            Database = "trains_test_" + Guid.NewGuid().ToString("N"),
            Pooling = false,
        };

        try {
            await using (var admin = new NpgsqlConnection(adminBuilder.ConnectionString)) {
                await admin.OpenAsync();
                await using var cmd = admin.CreateCommand();
                cmd.CommandText = $"CREATE DATABASE \"{testBuilder.Database}\";";
                await cmd.ExecuteNonQueryAsync();
            }

            var db = new PostgresTempDatabase(adminBuilder.ConnectionString, testBuilder.Database, testBuilder.ConnectionString);

            var options = new DbContextOptionsBuilder<TrainsDbContext>()
                .UseNpgsql(db.ConnectionString)
                .Options;

            await using (var ctx = new TrainsDbContext(options)) {
                await ctx.Database.MigrateAsync();
            }

            return db;
        }
        catch (Exception ex) {
            Skip.If(true, $"Postgres integration tests require a reachable PostgreSQL instance and permission to create/drop databases. {ex.Message}");
            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        var adminBuilder = new NpgsqlConnectionStringBuilder(this.AdminConnectionString);
        if (string.IsNullOrWhiteSpace(adminBuilder.Database))
            adminBuilder.Database = "postgres";

        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync();

        // Drop with FORCE if supported (Postgres 13+).
        try {
            await using var cmd = admin.CreateCommand();
            cmd.CommandText = $"DROP DATABASE \"{this.DatabaseName}\" WITH (FORCE);";
            await cmd.ExecuteNonQueryAsync();
            return;
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"DROP DATABASE WITH (FORCE) failed: {ex.Message}");
            // Best-effort fallback.
        }

        try {
            await using (var kill = admin.CreateCommand()) {
                kill.CommandText =
                    "SELECT pg_terminate_backend(pid) " +
                    "FROM pg_stat_activity " +
                    "WHERE datname = @db AND pid <> pg_backend_pid();";
                kill.Parameters.AddWithValue("db", this.DatabaseName);
                await kill.ExecuteNonQueryAsync();
            }

            await using (var drop = admin.CreateCommand()) {
                drop.CommandText = $"DROP DATABASE \"{this.DatabaseName}\";";
                await drop.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"Best-effort database cleanup failed: {ex.Message}");
            // Best-effort cleanup.
        }
    }
}
