using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace API.Interfaces.ShortLinks;

/// <summary>
/// The short-link settings (#844), read once from configuration. Everything but the connection string has a default in
/// appsettings.json's <c>ShortLinks</c> section, and the same default here when a key is missing.
/// </summary>
public sealed class ShortLinkSettings {
    /// <summary>The connection string name (<c>ConnectionStrings:ShortLinks</c>, or <c>ConnectionStrings__ShortLinks</c> in the environment).</summary>
    public const string ConnectionStringName = "ShortLinks";
    /// <summary>The configuration key for how many days a link lasts from when it is made.</summary>
    public const string LifetimeDaysKey = "ShortLinks:LifetimeDays";
    /// <summary>The configuration key for the largest request body, in bytes, a new link may have.</summary>
    public const string MaxViewBytesKey = "ShortLinks:MaxViewBytes";
    /// <summary>The configuration key for how many links one address may make an hour; zero or less turns the limit off.</summary>
    public const string CreatesPerHourKey = "ShortLinks:CreatesPerHourPerAddress";
    /// <summary>The configuration key for how often, in minutes, expired links are deleted.</summary>
    public const string CleanupMinutesKey = "ShortLinks:CleanupMinutes";

    /// <summary>The default lifetime: about six months.</summary>
    public const double DefaultLifetimeDays = 183;
    /// <summary>The default largest request body: 1 MiB.</summary>
    public const int DefaultMaxViewBytes = 1024 * 1024;
    /// <summary>The most <see cref="MaxViewBytesKey"/> may be set to; the share endpoint's request size limit sits just above it.</summary>
    public const int MaxViewBytesCeiling = 16 * 1024 * 1024;
    /// <summary>The default number of links one address may make an hour.</summary>
    public const int DefaultCreatesPerHour = 30;
    /// <summary>The default time between deletes of expired links, in minutes.</summary>
    public const double DefaultCleanupMinutes = 60;

    /// <summary>Reads the settings from <paramref name="configuration"/>.</summary>
    public ShortLinkSettings(IConfiguration configuration) {
        string? connectionString = configuration.GetConnectionString(ConnectionStringName);
        ConnectionString = string.IsNullOrWhiteSpace(connectionString) ? null : connectionString;
        double days = configuration.GetValue<double?>(LifetimeDaysKey) ?? DefaultLifetimeDays;
        Lifetime = TimeSpan.FromDays(days > 0 ? days : DefaultLifetimeDays);
        int maxBytes = configuration.GetValue<int?>(MaxViewBytesKey) ?? DefaultMaxViewBytes;
        MaxViewBytes = maxBytes > 0 ? Math.Min(maxBytes, MaxViewBytesCeiling) : DefaultMaxViewBytes;
        int creates = configuration.GetValue<int?>(CreatesPerHourKey) ?? DefaultCreatesPerHour;
        CreatesPerHour = creates > 0 ? creates : null;
        double minutes = configuration.GetValue<double?>(CleanupMinutesKey) ?? DefaultCleanupMinutes;
        CleanupInterval = TimeSpan.FromMinutes(minutes > 0 ? minutes : DefaultCleanupMinutes);
    }

    /// <summary>The PostgreSQL connection string, or null when no database is set up (the share endpoints answer 503).</summary>
    public string? ConnectionString { get; }
    /// <summary>How long a link lasts from when it is made. Opening a link does not extend it.</summary>
    public TimeSpan Lifetime { get; }
    /// <summary>The largest request body, in bytes, of a new link.</summary>
    public int MaxViewBytes { get; }
    /// <summary>How many links one address may make an hour, or null for no limit.</summary>
    public int? CreatesPerHour { get; }
    /// <summary>The time between deletes of expired links.</summary>
    public TimeSpan CleanupInterval { get; }
}

/// <summary>Short ids and content hashes of short links (#844).</summary>
public static class ShortLinkContent {
    /// <summary>The characters of a short id: base62.</summary>
    public const string IdAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    /// <summary>The length of a short id. 62^10 is about 8 * 10^17, so ids can't be guessed by trying them in turn.</summary>
    public const int IdLength = 10;

    /// <summary>
    /// A new random short id. <see cref="RandomNumberGenerator.GetString"/> picks each character from a cryptographic
    /// source without bias.
    /// </summary>
    public static string NewId() => RandomNumberGenerator.GetString(IdAlphabet, IdLength);

    /// <summary>Whether <paramref name="id"/> has the shape of a short id, checked before asking the store.</summary>
    public static bool IsWellFormedId(string? id) =>
        id is { Length: IdLength } && id.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z');

    /// <summary>
    /// The SHA-256 of <paramref name="view"/> written as minified JSON, which finds the same view again. Whitespace
    /// does not change it; the order of the keys does.
    /// </summary>
    public static byte[] Hash(JsonElement view) => SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(view)));
}
