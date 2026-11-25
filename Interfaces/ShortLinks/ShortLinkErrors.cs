namespace API.Interfaces.ShortLinks;

/// <summary>
/// The error codes and bodies of the share endpoints (#844), in the error contract of #933: <c>error</c> (the code),
/// <c>message</c> (what happened, in plain words), <c>detail</c>, <c>hint</c> (what to do), and numbers that help.
/// </summary>
public static class ShortLinkErrors {
    /// <summary>503: no short-link database is set up, or it could not be reached.</summary>
    public const string Unavailable = "short_links_unavailable";
    /// <summary>413: the request body is bigger than <see cref="ShortLinkSettings.MaxViewBytes"/>.</summary>
    public const string TooLarge = "share_too_large";
    /// <summary>400: the request body is not <c>{"view": {...}}</c> with a problem name in the view.</summary>
    public const string Invalid = "share_invalid";
    /// <summary>404: no link has this id, or it has expired, or the id is not a short id at all.</summary>
    public const string NotFound = "share_not_found";
    /// <summary>429: this address has made too many links this hour.</summary>
    public const string RateLimited = "share_rate_limited";

    /// <summary>The body of <see cref="Unavailable"/>.</summary>
    public static object UnavailableBody() => new Dictionary<string, object?> {
        ["error"] = Unavailable,
        ["message"] = "Share links aren't available on this server right now.",
        ["detail"] = "The server's link storage is not set up, or could not be reached.",
        ["hint"] = "Copy the page's address instead, or try again later.",
    };

    /// <summary>The body of <see cref="TooLarge"/>.</summary>
    public static object TooLargeBody(int maxViewBytes) => new Dictionary<string, object?> {
        ["error"] = TooLarge,
        ["message"] = "This view is too big to share as a link.",
        ["detail"] = $"A shared view can be at most {maxViewBytes} bytes.",
        ["maxViewBytes"] = maxViewBytes,
        ["hint"] = "Share a smaller instance, or save the instance to a file and share the file.",
    };

    /// <summary>The body of <see cref="Invalid"/>, with what was wrong in <paramref name="detail"/>.</summary>
    public static object InvalidBody(string detail) => new Dictionary<string, object?> {
        ["error"] = Invalid,
        ["message"] = "The view to share isn't in the expected form.",
        ["detail"] = detail,
        ["hint"] = "Send JSON like {\"view\": {\"problem\": \"SAT3\", \"instance\": \"...\"}}: \"view\" is an object, and its \"problem\" is the problem's name.",
    };

    /// <summary>The body of <see cref="NotFound"/>; the same for an unknown, expired or malformed id.</summary>
    public static object NotFoundBody() => new Dictionary<string, object?> {
        ["error"] = NotFound,
        ["message"] = "This share link doesn't exist or has expired.",
        ["detail"] = "No saved view has this id. Links last about six months from when they were made.",
        ["hint"] = "Ask whoever shared it for a new link.",
    };

    /// <summary>The body of <see cref="RateLimited"/>.</summary>
    public static object RateLimitedBody(int createsPerHour, int retryAfterSeconds) => new Dictionary<string, object?> {
        ["error"] = RateLimited,
        ["message"] = "Too many share links were made from this address recently.",
        ["detail"] = $"At most {createsPerHour} share links can be made from one address an hour.",
        ["createsPerHour"] = createsPerHour,
        ["retryAfterSeconds"] = retryAfterSeconds,
        ["hint"] = $"Wait about {retryAfterSeconds} seconds and try again. Links already made still open.",
    };
}
