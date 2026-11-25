using System.Net.Sockets;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace API.Interfaces.ShortLinks;

/// <summary>
/// The short-link store in PostgreSQL (#844), one row per link in the <c>share_links</c> table. The table and its
/// index are created at first use, so a new, empty database is all a deployment needs. Every database error is turned
/// into <see cref="ShortLinksUnavailableException"/>, so the share endpoints answer 503 while the database is down
/// and the rest of the API carries on.
/// </summary>
public sealed class PostgresShortLinkStore : IShortLinkStore {
    /// <summary>The table the links live in.</summary>
    public const string TableName = "share_links";

    /// <summary>How many new ids <see cref="CreateAsync"/> tries when an id is already taken by another view.</summary>
    internal const int IdAttempts = 5;

    private const string SchemaSql = $"""
        CREATE TABLE IF NOT EXISTS {TableName} (
            id text PRIMARY KEY,
            view jsonb NOT NULL,
            content_hash bytea NOT NULL UNIQUE,
            created_at timestamptz NOT NULL,
            expires_at timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS {TableName}_expires_at ON {TableName} (expires_at);
        """;

    // A view stored before keeps its id. If its link is still good, it keeps its creation time and its expiry moves out to
    // the new one if that is later; if its link expired, it comes back as if just made. So either way the link returned is
    // good for the whole lifetime. A new id that another view already has fails on the primary key, and CreateAsync tries
    // another.
    private const string CreateSql = $"""
        INSERT INTO {TableName} AS s (id, view, content_hash, created_at, expires_at)
        VALUES (@id, @view, @hash, @now, @expires)
        ON CONFLICT (content_hash) DO UPDATE SET
            created_at = CASE WHEN s.expires_at <= @now THEN EXCLUDED.created_at ELSE s.created_at END,
            expires_at = GREATEST(s.expires_at, EXCLUDED.expires_at)
        RETURNING id, created_at, expires_at
        """;

    private const string GetSql = $"SELECT id, view::text, created_at, expires_at FROM {TableName} WHERE id = @id AND expires_at > @now";

    private const string DeleteExpiredSql = $"DELETE FROM {TableName} WHERE expires_at <= @now";

    private readonly NpgsqlDataSource _dataSource;
    private readonly Func<string> _newId;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>Creates the store on <paramref name="dataSource"/>.</summary>
    public PostgresShortLinkStore(NpgsqlDataSource dataSource) : this(dataSource, ShortLinkContent.NewId) { }

    /// <summary>Creates the store with its own source of ids, so a test can make two views draw the same id.</summary>
    internal PostgresShortLinkStore(NpgsqlDataSource dataSource, Func<string> newId) {
        _dataSource = dataSource;
        _newId = newId;
    }

    /// <inheritdoc />
    public Task<ShortLinkCreated> CreateAsync(JsonElement view, DateTimeOffset now, TimeSpan lifetime, CancellationToken cancellationToken) =>
        Run(async () => {
            now = ToDatabaseTime(now);
            DateTimeOffset expires = ToDatabaseTime(now + lifetime);
            string json = JsonSerializer.Serialize(view);
            byte[] hash = ShortLinkContent.Hash(view);
            for (int attempt = 1; ; attempt++) {
                await using var command = _dataSource.CreateCommand(CreateSql);
                command.Parameters.AddWithValue("id", _newId());
                command.Parameters.AddWithValue("view", NpgsqlDbType.Jsonb, json);
                command.Parameters.AddWithValue("hash", hash);
                command.Parameters.AddWithValue("now", now);
                command.Parameters.AddWithValue("expires", expires);
                try {
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    await reader.ReadAsync(cancellationToken);
                    return new ShortLinkCreated(reader.GetString(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetFieldValue<DateTimeOffset>(2));
                } catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation && e.ConstraintName == $"{TableName}_pkey" && attempt < IdAttempts) {
                    // The new id is another view's. With 62^10 ids this is all but impossible, but cheap to handle.
                }
            }
        }, cancellationToken);

    /// <inheritdoc />
    public Task<ShortLink?> GetAsync(string id, DateTimeOffset now, CancellationToken cancellationToken) =>
        Run(async () => {
            await using var command = _dataSource.CreateCommand(GetSql);
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("now", ToDatabaseTime(now));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;
            using var view = JsonDocument.Parse(reader.GetString(1));
            return new ShortLink(reader.GetString(0), view.RootElement.Clone(),
                reader.GetFieldValue<DateTimeOffset>(2), reader.GetFieldValue<DateTimeOffset>(3));
        }, cancellationToken);

    /// <inheritdoc />
    public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        Run(async () => {
            await using var command = _dataSource.CreateCommand(DeleteExpiredSql);
            command.Parameters.AddWithValue("now", ToDatabaseTime(now));
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);

    /// <summary>
    /// Creates the table the first time it is needed. Only one request does it at a time, and once it worked it is not
    /// done again; if it failed, the next request tries again (the database may have been down).
    /// </summary>
    private async Task EnsureSchemaAsync(CancellationToken cancellationToken) {
        if (_schemaReady)
            return;
        await _schemaLock.WaitAsync(cancellationToken);
        try {
            if (_schemaReady)
                return;
            await using var command = _dataSource.CreateCommand(SchemaSql);
            await command.ExecuteNonQueryAsync(cancellationToken);
            _schemaReady = true;
        } finally {
            _schemaLock.Release();
        }
    }

    /// <summary>Runs <paramref name="work"/> once the table exists, turning database errors into <see cref="ShortLinksUnavailableException"/>.</summary>
    private async Task<T> Run<T>(Func<Task<T>> work, CancellationToken cancellationToken) {
        try {
            await EnsureSchemaAsync(cancellationToken);
            return await work();
        } catch (Exception e) when (e is NpgsqlException or SocketException or TimeoutException && !cancellationToken.IsCancellationRequested) {
            throw new ShortLinksUnavailableException("The short-link database could not be reached or failed.", e);
        }
    }

    /// <summary>PostgreSQL keeps microseconds in UTC, so times are rounded down to that before they are stored or compared.</summary>
    private static DateTimeOffset ToDatabaseTime(DateTimeOffset time) {
        var utc = time.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
    }
}
