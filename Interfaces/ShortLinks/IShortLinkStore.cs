using System.Text.Json;

namespace API.Interfaces.ShortLinks;

/// <summary>
/// Where short links live (#844): a short id that stands for one view a frontend shows (the problem, its instance, the
/// solver, the reduction, ...), so a view too big for a URL can still be shared. The API answers the share endpoints
/// through this interface, so it runs without a database (<see cref="UnavailableShortLinkStore"/>), with PostgreSQL
/// (<see cref="PostgresShortLinkStore"/>), or with a test double.
/// <para>
/// Every method takes the current time, so expiry can be tested without waiting, and throws
/// <see cref="ShortLinksUnavailableException"/> when the store can't be reached.
/// </para>
/// </summary>
public interface IShortLinkStore {
    /// <summary>
    /// Stores <paramref name="view"/> and returns its link. A view that already has a link gets the same link back
    /// (the same view is found by the SHA-256 of its minified JSON, <see cref="ShortLinkContent.Hash"/>), and that
    /// link's expiry is pushed out to <paramref name="now"/> + <paramref name="lifetime"/> if that is later. A link
    /// whose view was stored before but has expired is brought back with a new creation time. So the link returned is
    /// always good for the whole lifetime.
    /// </summary>
    Task<ShortLinkCreated> CreateAsync(JsonElement view, DateTimeOffset now, TimeSpan lifetime, CancellationToken cancellationToken);

    /// <summary>The link with <paramref name="id"/>, or null when there is none or it expired at or before <paramref name="now"/>.</summary>
    Task<ShortLink?> GetAsync(string id, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Deletes every link that expired at or before <paramref name="now"/>, and returns how many it deleted.</summary>
    Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>A link just made (or found again) by <see cref="IShortLinkStore.CreateAsync"/>.</summary>
/// <param name="Id">The short id.</param>
/// <param name="CreatedAt">When the link was made.</param>
/// <param name="ExpiresAt">When the link stops working.</param>
public sealed record ShortLinkCreated(string Id, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

/// <summary>A stored link and the view it stands for.</summary>
/// <param name="Id">The short id.</param>
/// <param name="View">The view, a JSON object.</param>
/// <param name="CreatedAt">When the link was made.</param>
/// <param name="ExpiresAt">When the link stops working.</param>
public sealed record ShortLink(string Id, JsonElement View, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

/// <summary>The short-link store is not set up, or could not be reached. The share endpoints answer 503.</summary>
public sealed class ShortLinksUnavailableException : Exception {
    /// <summary>Creates the exception with a message for the log, and the error that caused it, if any.</summary>
    public ShortLinksUnavailableException(string message, Exception? innerException = null) : base(message, innerException) { }
}

/// <summary>The store used when no database is set up (no <c>ConnectionStrings:ShortLinks</c>): every call throws.</summary>
public sealed class UnavailableShortLinkStore : IShortLinkStore {
    private static ShortLinksUnavailableException NotSetUp() =>
        new($"No short-link database is set up (connection string {ShortLinkSettings.ConnectionStringName} is not set).");

    /// <inheritdoc />
    public Task<ShortLinkCreated> CreateAsync(JsonElement view, DateTimeOffset now, TimeSpan lifetime, CancellationToken cancellationToken) =>
        throw NotSetUp();

    /// <inheritdoc />
    public Task<ShortLink?> GetAsync(string id, DateTimeOffset now, CancellationToken cancellationToken) => throw NotSetUp();

    /// <inheritdoc />
    public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) => throw NotSetUp();
}
