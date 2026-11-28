using System.Text.Json;
using API.Interfaces.ShortLinks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

/// <summary>
/// Short links to views (#844): a frontend saves the view it shows (the problem, its instance, the solver, the
/// reduction, ...) and gets a short id back, so the view can be shared even when its instance is too big for a URL.
/// The API only keeps the data; each frontend has its own page that opens a link. Links last about six months
/// (<c>ShortLinks:LifetimeDays</c>) from when they were made. Without a database (<c>ConnectionStrings:ShortLinks</c>)
/// both endpoints answer 503 <c>short_links_unavailable</c>.
/// </summary>
[ApiController]
[Route("[controller]")]
[Tags("Share Links")]
public class ShareLinksController : ControllerBase {
    /// <summary>How deeply a view may nest objects and arrays, counting the <c>{"view": ...}</c> around it.</summary>
    public const int MaxDepth = 64;

    private readonly IShortLinkStore _store;
    private readonly ShortLinkSettings _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<ShareLinksController> _logger;

    /// <summary>Creates the controller.</summary>
    public ShareLinksController(IShortLinkStore store, ShortLinkSettings settings, TimeProvider time, ILogger<ShareLinksController> logger) {
        _store = store;
        _settings = settings;
        _time = time;
        _logger = logger;
    }

    /// <summary>
    /// Saves a view and returns its short link. The body is JSON <c>{"view": {...}}</c>: <c>view</c> is an object with
    /// any keys the frontend needs, and a <c>problem</c> key with the problem's name. Saving the same view again
    /// returns the same id, and the link then lasts the full lifetime from now.
    /// </summary>
    /// <response code="201">The link was made (or found again): <c>{"id", "createdAt", "expiresAt"}</c>, with a <c>Location</c> of <c>/ShareLinks/{id}</c>.</response>
    /// <response code="400"><c>share_invalid</c>: the body is not valid JSON, <c>view</c> is not an object, or it has no <c>problem</c> name.</response>
    /// <response code="413"><c>share_too_large</c>: the body is bigger than <c>ShortLinks:MaxViewBytes</c> (1 MiB by default).</response>
    /// <response code="429"><c>share_rate_limited</c>: this address made too many links this hour; see the <c>Retry-After</c> header.</response>
    /// <response code="503"><c>short_links_unavailable</c>: no link database is set up, or it could not be reached.</response>
    [HttpPost]
    [EnableRateLimiting(ShortLinkServices.CreatePolicy)]
    // A backstop: the body is read with the configured cap below, which may be at most this much.
    [RequestSizeLimit(ShortLinkSettings.MaxViewBytesCeiling + 1024)]
    [ProducesResponseType(typeof(ShortLinkCreated), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create() {
        var cancellationToken = HttpContext.RequestAborted;
        byte[]? body = await ReadBodyAsync(cancellationToken);
        if (body is null)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, ShortLinkErrors.TooLargeBody(_settings.MaxViewBytes));

        JsonDocument document;
        try {
            document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = MaxDepth, AllowDuplicateProperties = false });
        } catch (JsonException e) {
            return BadRequest(ShortLinkErrors.InvalidBody($"The body is not valid JSON (at most {MaxDepth} levels deep, no repeated keys): {e.Message}"));
        }
        using (document) {
            if (Validate(document.RootElement) is { } problem)
                return BadRequest(ShortLinkErrors.InvalidBody(problem));
            var view = document.RootElement.GetProperty("view");
            try {
                var created = await _store.CreateAsync(view, _time.GetUtcNow(), _settings.Lifetime, cancellationToken);
                return Created($"/ShareLinks/{created.Id}", created);
            } catch (ShortLinksUnavailableException e) {
                return Unavailable(e);
            }
        }
    }

    /// <summary>
    /// Returns the view a short link was made for, as the JSON object that was saved. Opening a link does not extend it.
    /// Key order inside the view may differ from what was sent.
    /// </summary>
    /// <param name="id">The link's short id: 10 letters and digits.</param>
    /// <response code="200"><c>{"id", "view", "createdAt", "expiresAt"}</c>.</response>
    /// <response code="404"><c>share_not_found</c>: no link has this id, it has expired, or the id is not a short id.</response>
    /// <response code="503"><c>short_links_unavailable</c>: no link database is set up, or it could not be reached.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ShortLink), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(string id) {
        if (!ShortLinkContent.IsWellFormedId(id))
            return NotFound(ShortLinkErrors.NotFoundBody());
        try {
            var link = await _store.GetAsync(id, _time.GetUtcNow(), HttpContext.RequestAborted);
            return link is null ? NotFound(ShortLinkErrors.NotFoundBody()) : Ok(link);
        } catch (ShortLinksUnavailableException e) {
            return Unavailable(e);
        }
    }

    private ObjectResult Unavailable(ShortLinksUnavailableException e) {
        if (e.InnerException is not null)
            _logger.LogWarning(e, "The short-link store failed.");
        return StatusCode(StatusCodes.Status503ServiceUnavailable, ShortLinkErrors.UnavailableBody());
    }

    /// <summary>
    /// The request body, or null when it is bigger than <see cref="ShortLinkSettings.MaxViewBytes"/>. A body that says
    /// it is too big is not read at all, and one without a length is read only up to one byte past the cap.
    /// </summary>
    private async Task<byte[]?> ReadBodyAsync(CancellationToken cancellationToken) {
        int max = _settings.MaxViewBytes;
        if (Request.ContentLength > max)
            return null;
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[16 * 1024];
        try {
            int read;
            while ((read = await Request.Body.ReadAsync(chunk, cancellationToken)) > 0) {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > max)
                    return null;
            }
        } catch (BadHttpRequestException e) when (e.StatusCode == StatusCodes.Status413PayloadTooLarge) {
            return null;
        }
        return buffer.ToArray();
    }

    /// <summary>What is wrong with the body <paramref name="root"/>, or null when it is a view that can be saved.</summary>
    internal static string? Validate(JsonElement root) {
        if (root.ValueKind != JsonValueKind.Object)
            return $"The body must be a JSON object with a \"view\" key, but it is {Describe(root.ValueKind)}.";
        if (!root.TryGetProperty("view", out var view))
            return "The body has no \"view\" key.";
        if (view.ValueKind != JsonValueKind.Object)
            return $"\"view\" must be a JSON object, but it is {Describe(view.ValueKind)}.";
        if (!view.TryGetProperty("problem", out var problem) || problem.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(problem.GetString()))
            return "\"view\" must have a \"problem\" key whose value is the problem's name, such as \"SAT3\".";
        if (HasNullCharacter(view))
            return "The view has a text value or key with the character \\u0000, which can't be saved.";
        return null;
    }

    private static string Describe(JsonValueKind kind) => kind switch {
        JsonValueKind.Array => "an array",
        JsonValueKind.String => "a string",
        JsonValueKind.Number => "a number",
        JsonValueKind.True or JsonValueKind.False => "true or false",
        _ => "null",
    };

    // PostgreSQL's jsonb can't hold the character U+0000, so a view with one is refused here instead of failing in the store.
    private static bool HasNullCharacter(JsonElement element) => element.ValueKind switch {
        JsonValueKind.Object => element.EnumerateObject().Any(p => p.Name.Contains('\0') || HasNullCharacter(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Any(HasNullCharacter),
        JsonValueKind.String => element.GetString()!.Contains('\0'),
        _ => false,
    };
}
