using System.Reflection;
using Dapper;
using Microsoft.Data.Sqlite;

namespace MsgBakMan.Data.Sqlite;

public sealed class MigrationRunner
{
    private readonly Assembly _assembly;

    public MigrationRunner(Assembly? migrationsAssembly = null)
    {
        _assembly = migrationsAssembly ?? typeof(MigrationRunner).Assembly;
    }

    public void ApplyAll(SqliteConnection connection)
    {
        connection.Execute(@"
CREATE TABLE IF NOT EXISTS schema_migrations (
  version INTEGER PRIMARY KEY,
  applied_utc INTEGER NOT NULL
);
");

        var applied = new HashSet<int>(connection.Query<int>("SELECT version FROM schema_migrations"));

        foreach (var migration in GetMigrations())
        {
            if (applied.Contains(migration.Version))
            {
                continue;
            }

            using var tx = connection.BeginTransaction();
            connection.Execute(migration.Sql, transaction: tx);
            connection.Execute(
                "INSERT INTO schema_migrations(version, applied_utc) VALUES (@v, @t)",
                new { v = migration.Version, t = DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                transaction: tx);
            tx.Commit();
        }
    }

    private IEnumerable<(int Version, string Name, string Sql)> GetMigrations()
    {
        var resources = _assembly
            .GetManifestResourceNames()
            .Where(n => n.Contains(".Migrations.") && n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        foreach (var res in resources)
        {
            var fileName = res.Split('.').Reverse().Skip(1).FirstOrDefault() is string last
                ? last
                : res;

            // Expected pattern: 0001_init.sql
            var baseName = res[(res.LastIndexOf(".Migrations.", StringComparison.Ordinal) + ".Migrations.".Length)..];
            var versionText = baseName.Split('_').FirstOrDefault() ?? "0";
            _ = int.TryParse(versionText, out var version);

            using var stream = _assembly.GetManifestResourceStream(res);
            if (stream is null)
            {
                continue;
            }
            using var reader = new StreamReader(stream);
            var sql = reader.ReadToEnd();

            yield return (version, fileName, sql);
        }
    }
}
