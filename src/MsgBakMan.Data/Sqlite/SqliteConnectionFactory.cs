using Microsoft.Data.Sqlite;

namespace MsgBakMan.Data.Sqlite;

public sealed class SqliteConnectionFactory
{
    private readonly string _dbPath;

    public SqliteConnectionFactory(string dbPath)
    {
        _dbPath = dbPath;
    }

    public SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath) ?? ".");

        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };

        var conn = new SqliteConnection(csb.ToString());
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL;";
        cmd.ExecuteNonQuery();

        return conn;
    }
}
