using System.Text.Json;
using API.Interfaces.ShortLinks;

namespace redux_tests;
#pragma warning disable CS1591

// A clock the test moves by hand, so a link can expire without waiting six months. Only the time it reports is fake;
// timers still run on the real clock.
public sealed class ManualTimeProvider : TimeProvider {
    private DateTimeOffset _now;

    public ManualTimeProvider(DateTimeOffset start) {
        _now = start;
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

// The short-link store in memory, for the endpoint tests (#844): the same rules as PostgresShortLinkStore (the same view
// gets the same id, a link found again lasts the full lifetime, an expired one comes back as new) without a database.
public sealed class InMemoryShortLinkStore : IShortLinkStore {
    private readonly object _lock = new();
    private readonly Dictionary<string, (ShortLink Link, string Hash)> _byId = [];
    private readonly Dictionary<string, string> _idByHash = [];

    public int DeleteCalls { get; private set; }

    public Task<ShortLinkCreated> CreateAsync(JsonElement view, DateTimeOffset now, TimeSpan lifetime, CancellationToken cancellationToken) {
        string hash = Convert.ToHexString(ShortLinkContent.Hash(view));
        lock (_lock) {
            ShortLink link;
            if (_idByHash.TryGetValue(hash, out var id)) {
                var old = _byId[id].Link;
                link = old.ExpiresAt <= now
                    ? old with { CreatedAt = now, ExpiresAt = now + lifetime }
                    : old with { ExpiresAt = old.ExpiresAt > now + lifetime ? old.ExpiresAt : now + lifetime };
            } else {
                link = new ShortLink(ShortLinkContent.NewId(), view.Clone(), now, now + lifetime);
                _idByHash[hash] = link.Id;
            }
            _byId[link.Id] = (link, hash);
            return Task.FromResult(new ShortLinkCreated(link.Id, link.CreatedAt, link.ExpiresAt));
        }
    }

    public Task<ShortLink?> GetAsync(string id, DateTimeOffset now, CancellationToken cancellationToken) {
        lock (_lock)
            return Task.FromResult(_byId.TryGetValue(id, out var entry) && entry.Link.ExpiresAt > now ? entry.Link : null);
    }

    public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) {
        lock (_lock) {
            DeleteCalls++;
            var expired = _byId.Values.Where(e => e.Link.ExpiresAt <= now).ToList();
            foreach (var (link, hash) in expired) {
                _byId.Remove(link.Id);
                _idByHash.Remove(hash);
            }
            return Task.FromResult(expired.Count);
        }
    }
}

// A store whose every call fails, as a database that went down would.
public sealed class FailingShortLinkStore : IShortLinkStore {
    private static ShortLinksUnavailableException Down() => new("down", new InvalidOperationException("the database is down"));

    public Task<ShortLinkCreated> CreateAsync(JsonElement view, DateTimeOffset now, TimeSpan lifetime, CancellationToken cancellationToken) => throw Down();

    public Task<ShortLink?> GetAsync(string id, DateTimeOffset now, CancellationToken cancellationToken) => throw Down();

    public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) => throw Down();
}
