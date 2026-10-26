using Dapper;
using Microsoft.Data.Sqlite;

namespace MapleHelper.Infrastructure.Persistence;

/// <summary>SQLite trong %AppData%\MapleClassicHelper\data.db. Tạo bảng và nâng cấp schema theo PRAGMA user_version.</summary>
public sealed class AppDatabase
{
    private const int SchemaVersion = 1;

    private readonly string _connectionString;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public AppDatabase(AppPaths paths)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabaseFile,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    public async Task<SqliteConnection> OpenAsync(CancellationToken ct = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        if (!_initialized)
        {
            await InitializeAsync(connection, ct);
        }
        return connection;
    }

    private async Task InitializeAsync(SqliteConnection connection, CancellationToken ct)
    {
        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized)
            {
                return;
            }

            var version = await connection.ExecuteScalarAsync<long>("PRAGMA user_version");
            if (version < 1)
            {
                await connection.ExecuteAsync("""
                    PRAGMA journal_mode = WAL;

                    CREATE TABLE IF NOT EXISTS news_articles (
                        id INTEGER PRIMARY KEY,
                        category TEXT NOT NULL,
                        title TEXT NOT NULL,
                        summary TEXT NOT NULL,
                        image_url TEXT,
                        live_date TEXT NOT NULL,
                        is_classic INTEGER NOT NULL,
                        featured INTEGER NOT NULL,
                        title_vi TEXT,
                        summary_vi TEXT,
                        body_html TEXT,
                        body_vi TEXT,
                        is_saved INTEGER NOT NULL DEFAULT 0,
                        fetched_at TEXT NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS ix_news_live_date ON news_articles (live_date DESC);

                    CREATE TABLE IF NOT EXISTS history_entries (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        kind TEXT NOT NULL,
                        title TEXT,
                        input TEXT NOT NULL,
                        result TEXT NOT NULL,
                        sources_json TEXT,
                        image_png BLOB,
                        created_at TEXT NOT NULL,
                        is_saved INTEGER NOT NULL DEFAULT 0
                    );
                    CREATE INDEX IF NOT EXISTS ix_history_created ON history_entries (created_at DESC);
                    """);
            }

            // Bản sau thêm cột/bảng: if (version < 2) { ... }

            await connection.ExecuteAsync($"PRAGMA user_version = {SchemaVersion}");
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
