using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace API.Interfaces.ShortLinks;

/// <summary>Registers the short links (#844): the settings, the store, the hourly cleanup and the limit on new links.</summary>
public static class ShortLinkServices {
    /// <summary>The rate-limiting policy on making links (<c>POST /ShareLinks</c>); nothing else uses it.</summary>
    public const string CreatePolicy = "share-link-create";

    /// <summary>
    /// Adds the short-link services. With a <c>ConnectionStrings:ShortLinks</c> the links live in PostgreSQL
    /// (<see cref="PostgresShortLinkStore"/>); without one the share endpoints answer 503 and nothing else changes.
    /// The settings are read when the app starts, from the app's own configuration, so tests can change them.
    /// </summary>
    public static IServiceCollection AddShortLinks(this IServiceCollection services) {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(provider => new ShortLinkSettings(provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton(provider => {
            var settings = provider.GetRequiredService<ShortLinkSettings>();
            var builder = new NpgsqlDataSourceBuilder(settings.ConnectionString ?? "");
            builder.UseLoggerFactory(provider.GetService<ILoggerFactory>());
            return builder.Build();
        });
        services.AddSingleton<IShortLinkStore>(provider => provider.GetRequiredService<ShortLinkSettings>().ConnectionString is null
            ? new UnavailableShortLinkStore()
            : new PostgresShortLinkStore(provider.GetRequiredService<NpgsqlDataSource>()));
        services.AddHostedService<ShortLinkCleanupService>();

        services.AddRateLimiter(options => {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Partitioned by the address the request came from. Behind a reverse proxy (nginx) that is the proxy's address,
            // so every visitor shares one limit until forwarded headers are trusted; that is the deployment's decision and
            // is deliberately not made here.
            options.AddPolicy(CreatePolicy, context => {
                var settings = context.RequestServices.GetRequiredService<ShortLinkSettings>();
                string address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return settings.CreatesPerHour is not { } limit
                    ? RateLimitPartition.GetNoLimiter(address)
                    : RateLimitPartition.GetFixedWindowLimiter(address, _ => new FixedWindowRateLimiterOptions {
                        PermitLimit = limit,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                    });
            });
            options.OnRejected = async (context, cancellationToken) => {
                int seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                    : 3600;
                var settings = context.HttpContext.RequestServices.GetRequiredService<ShortLinkSettings>();
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                await context.HttpContext.Response.WriteAsJsonAsync(ShortLinkErrors.RateLimitedBody(settings.CreatesPerHour ?? 0, seconds), cancellationToken);
            };
        });
        return services;
    }
}

/// <summary>
/// Deletes expired short links every <see cref="ShortLinkSettings.CleanupInterval"/> (about hourly), when a database is
/// set up (#844). Expired links already answer 404; this only keeps the table from growing. A failed delete is logged
/// and tried again next time, and never stops the app.
/// </summary>
public sealed class ShortLinkCleanupService : BackgroundService {
    private readonly IShortLinkStore _store;
    private readonly ShortLinkSettings _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<ShortLinkCleanupService> _logger;

    /// <summary>Creates the service.</summary>
    public ShortLinkCleanupService(IShortLinkStore store, ShortLinkSettings settings, TimeProvider time, ILogger<ShortLinkCleanupService> logger) {
        _store = store;
        _settings = settings;
        _time = time;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if (_store is UnavailableShortLinkStore)
            return;
        using var timer = new PeriodicTimer(_settings.CleanupInterval, _time);
        try {
            do {
                await CleanUpOnceAsync(stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            // The app is stopping.
        }
    }

    /// <summary>Deletes the expired links once; returns how many, or null when the store failed (the failure is logged).</summary>
    internal async Task<int?> CleanUpOnceAsync(CancellationToken cancellationToken) {
        try {
            int deleted = await _store.DeleteExpiredAsync(_time.GetUtcNow(), cancellationToken);
            if (deleted > 0)
                _logger.LogInformation("Deleted {Count} expired short links.", deleted);
            return deleted;
        } catch (Exception e) when (e is not OperationCanceledException) {
            _logger.LogWarning(e, "Deleting expired short links failed; trying again in {Interval}.", _settings.CleanupInterval);
            return null;
        }
    }
}
