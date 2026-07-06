using Npgsql;

namespace dockertest.Services;

public static class PostgreSqlBootstrap
{
    public static async Task EnsureDatabaseExistsAsync(string connectionString, CancellationToken ct = default)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var database = builder.Database;
        if (string.IsNullOrWhiteSpace(database))
            return;

        builder.Database = "postgres";
        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync(ct);

        await using var check = new NpgsqlCommand(
            "SELECT 1 FROM pg_database WHERE datname = @name", conn);
        check.Parameters.AddWithValue("name", database);
        var exists = await check.ExecuteScalarAsync(ct) != null;
        if (exists)
            return;

        var safeName = database.Replace("\"", "\"\"");
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{safeName}\"", conn);
        await create.ExecuteNonQueryAsync(ct);
    }
}
