using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces.ShortLinks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The app with the short-link store in memory and a clock the test moves (#844). The rate limit is off and the size cap
// small, so the tests can make many links and reach the cap cheaply; a test that needs other settings derives its own.
public class ShareLinksAppFactory : AppFactory {
    public const int MaxViewBytes = 2048;

    public InMemoryShortLinkStore Store { get; } = new();
    public ManualTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));

    protected virtual Dictionary<string, string?> Settings => new() {
        [ShortLinkSettings.MaxViewBytesKey] = MaxViewBytes.ToString(),
        [ShortLinkSettings.CreatesPerHourKey] = "0",
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(Settings));
        builder.ConfigureTestServices(services => {
            services.RemoveAll<IShortLinkStore>();
            services.AddSingleton<IShortLinkStore>(Store);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }
}

// The same app with a limit of two new links an hour per address.
public class RateLimitedShareLinksAppFactory : ShareLinksAppFactory {
    public const int Limit = 2;

    protected override Dictionary<string, string?> Settings => new() { [ShortLinkSettings.CreatesPerHourKey] = Limit.ToString() };
}

// Helpers shared by the share-link endpoint tests.
internal static class ShareLinkRequests {
    public static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    // A view nobody else in the test run saves, so tests sharing a store don't find each other's links.
    public static string NewView() => "{\"view\":{\"problem\":\"SAT3\",\"instance\":\"" + Guid.NewGuid() + "\",\"solver\":\"SAT3BruteForceSolver\"}}";

    public static async Task<JsonElement> ReadJson(this HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement.Clone();

    public static async Task<string> ErrorCode(this HttpResponseMessage response) =>
        (await response.ReadJson()).GetProperty("error").GetString()!;
}

// POST /ShareLinks and GET /ShareLinks/{id} (#844), against the in-memory store: the round trip, the same view getting the
// same link, expiry, and every refusal (400, 404, 413).
public class ShareLinks_Endpoint_Tests : IClassFixture<ShareLinksAppFactory> {
    private readonly ShareLinksAppFactory _factory;
    private readonly HttpClient _client;
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ShareLinks_Endpoint_Tests(ShareLinksAppFactory factory) {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<JsonElement> Create(string body) {
        var response = await _client.PostAsync("/ShareLinks", ShareLinkRequests.Json(body), Token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadJson();
    }

    [Fact]
    public async Task Create_Answers201_WithTheIdTimesAndLocation() {
        var response = await _client.PostAsync("/ShareLinks", ShareLinkRequests.Json(ShareLinkRequests.NewView()), Token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJson();
        string id = body.GetProperty("id").GetString()!;
        Assert.True(ShortLinkContent.IsWellFormedId(id));
        Assert.Equal($"/ShareLinks/{id}", response.Headers.Location?.OriginalString);
        var created = body.GetProperty("createdAt").GetDateTimeOffset();
        Assert.Equal(_factory.Time.GetUtcNow(), created);
        Assert.Equal(created + TimeSpan.FromDays(ShortLinkSettings.DefaultLifetimeDays), body.GetProperty("expiresAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task CreateThenGet_ReturnsTheSavedViewAsAJsonObject() {
        string view = """{"problem":"CLIQUE","instance":"{{1,2},{{1,2}},2}","solver":"CliqueBruteForce","reduceTo":"VERTEXCOVER","step":3,"options":{"seed":null,"weights":[1,2.5]}}""";
        string id = (await Create("{\"view\": " + view + " }")).GetProperty("id").GetString()!;

        var response = await _client.GetAsync($"/ShareLinks/{id}", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJson();
        Assert.Equal(id, body.GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Object, body.GetProperty("view").ValueKind);
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(view).RootElement, body.GetProperty("view")));
        Assert.True(body.GetProperty("expiresAt").GetDateTimeOffset() > body.GetProperty("createdAt").GetDateTimeOffset());
    }

    // Saving a view twice gives the same link, and the link then lasts the full lifetime from the second save.
    [Fact]
    public async Task Create_SameViewAgain_ReturnsTheSameId_AndPushesTheExpiryOut() {
        string view = ShareLinkRequests.NewView();
        var first = await Create(view);
        _factory.Time.Advance(TimeSpan.FromDays(10));
        var second = await Create(view);

        Assert.Equal(first.GetProperty("id").GetString(), second.GetProperty("id").GetString());
        Assert.Equal(first.GetProperty("createdAt").GetDateTimeOffset(), second.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(first.GetProperty("expiresAt").GetDateTimeOffset() + TimeSpan.FromDays(10), second.GetProperty("expiresAt").GetDateTimeOffset());
    }

    // The same view is found by its minified JSON, so spacing doesn't make a new link.
    [Fact]
    public async Task Create_SameViewWithOtherSpacing_ReturnsTheSameId() {
        string instance = Guid.NewGuid().ToString();
        var first = await Create("{\"view\":{\"problem\":\"SAT3\",\"instance\":\"" + instance + "\"}}");
        var second = await Create($$"""{ "view" : { "problem" : "SAT3",   "instance" : "{{instance}}" } }""");
        Assert.Equal(first.GetProperty("id").GetString(), second.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Create_DifferentViews_GetDifferentIds() {
        var first = await Create(ShareLinkRequests.NewView());
        var second = await Create(ShareLinkRequests.NewView());
        Assert.NotEqual(first.GetProperty("id").GetString(), second.GetProperty("id").GetString());
    }

    [Theory]
    [InlineData("AAAAAAAAAA")] // the right shape, but no link has it
    [InlineData("abc")] // too short
    [InlineData("abcdefghijk")] // too long
    [InlineData("abcde-ghij")] // not base62
    public async Task Get_UnknownOrMalformedId_Answers404(string id) {
        var response = await _client.GetAsync($"/ShareLinks/{id}", Token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ShortLinkErrors.NotFound, await response.ErrorCode());
    }

    // An expired link answers just like one that never existed, and opening a link doesn't extend it.
    [Fact]
    public async Task Get_ExpiredLink_Answers404() {
        string id = (await Create(ShareLinkRequests.NewView())).GetProperty("id").GetString()!;
        _factory.Time.Advance(TimeSpan.FromDays(ShortLinkSettings.DefaultLifetimeDays) - TimeSpan.FromSeconds(1));
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/ShareLinks/{id}", Token)).StatusCode);

        _factory.Time.Advance(TimeSpan.FromSeconds(1));
        var response = await _client.GetAsync($"/ShareLinks/{id}", Token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ShortLinkErrors.NotFound, await response.ErrorCode());
    }

    // An expired view saved again comes back for a whole new lifetime.
    [Fact]
    public async Task Create_ExpiredViewAgain_ComesBackForTheFullLifetime() {
        string view = ShareLinkRequests.NewView();
        string id = (await Create(view)).GetProperty("id").GetString()!;
        _factory.Time.Advance(TimeSpan.FromDays(ShortLinkSettings.DefaultLifetimeDays + 1));
        var again = await Create(view);

        Assert.Equal(id, again.GetProperty("id").GetString());
        Assert.Equal(_factory.Time.GetUtcNow(), again.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/ShareLinks/{id}", Token)).StatusCode);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"view": {"problem": "SAT3"}""")] // cut short
    [InlineData("[1, 2]")]
    [InlineData("\"view\"")]
    [InlineData("{}")]
    [InlineData("""{"View": {"problem": "SAT3"}}""")] // keys are case-sensitive
    [InlineData("""{"view": null}""")]
    [InlineData("""{"view": 5}""")]
    [InlineData("""{"view": "SAT3"}""")]
    [InlineData("""{"view": true}""")]
    [InlineData("""{"view": [{"problem": "SAT3"}]}""")]
    [InlineData("""{"view": {}}""")]
    [InlineData("""{"view": {"instance": "x"}}""")]
    [InlineData("""{"view": {"problem": ""}}""")]
    [InlineData("""{"view": {"problem": "   "}}""")]
    [InlineData("""{"view": {"problem": 3}}""")]
    [InlineData("""{"view": {"problem": "SAT3", "problem": "CLIQUE"}}""")] // a repeated key
    [InlineData("""{"view": {"problem": "SAT3", "instance": "a\u0000b"}}""")] // PostgreSQL can't keep U+0000
    [InlineData("""{"view": {"problem": "SAT3", "a\u0000": 1}}""")]
    [InlineData("""{"view": {"problem": "SAT3", "list": [["a\u0000"]]}}""")]
    public async Task Create_BodyThatIsNotAView_Answers400(string body) {
        var response = await _client.PostAsync("/ShareLinks", ShareLinkRequests.Json(body), Token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.ReadJson();
        Assert.Equal(ShortLinkErrors.Invalid, error.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("detail").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("hint").GetString()));
    }

    // Nesting past the limit is refused; just under it is saved.
    [Fact]
    public async Task Create_NestedTooDeep_Answers400_AndJustUnderIsSaved() {
        static string Nested(int depth) => "{\"view\":{\"problem\":\"SAT3\",\"deep\":" + new string('[', depth) + new string(']', depth) + "}}";
        // The body object and the view take two levels.
        var tooDeep = await _client.PostAsync("/ShareLinks", ShareLinkRequests.Json(Nested(ShareLinksController.MaxDepth - 1)), Token);
        Assert.Equal(HttpStatusCode.BadRequest, tooDeep.StatusCode);
        Assert.Equal(ShortLinkErrors.Invalid, await tooDeep.ErrorCode());

        var deepest = await _client.PostAsync("/ShareLinks", ShareLinkRequests.Json(Nested(ShareLinksController.MaxDepth - 2)), Token);
        Assert.Equal(HttpStatusCode.Created, deepest.StatusCode);
    }

    // A body of exactly `bytes` bytes, padded out with one string.
    private static string BodyOfSize(int bytes) {
        string start = "{\"view\":{\"problem\":\"SAT3\",\"instance\":\"" + Guid.NewGuid() + "\",\"pad\":\"";
        const string end = "\"}}";
        return start + new string('x', bytes - start.Length - end.Length) + end;
    }

    [Fact]
    public async Task Create_BodyAtTheCap_IsSaved() {
        string body = BodyOfSize(ShareLinksAppFactory.MaxViewBytes);
        Assert.Equal(ShareLinksAppFactory.MaxViewBytes, Encoding.UTF8.GetByteCount(body));
        var response = await _client.PostAsync("/ShareLinks", ShareLinkRequests.Json(body), Token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_BodyOneByteOverTheCap_Answers413() {
        var response = await _client.PostAsync("/ShareLinks", ShareLinkRequests.Json(BodyOfSize(ShareLinksAppFactory.MaxViewBytes + 1)), Token);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        var error = await response.ReadJson();
        Assert.Equal(ShortLinkErrors.TooLarge, error.GetProperty("error").GetString());
        Assert.Equal(ShareLinksAppFactory.MaxViewBytes, error.GetProperty("maxViewBytes").GetInt32());
    }

    // A body sent without a Content-Length (chunked) is read only up to the cap, then refused.
    [Fact]
    public async Task Create_BodyWithoutLengthOverTheCap_Answers413() {
        var content = new UnknownLengthContent(Encoding.UTF8.GetBytes(BodyOfSize(ShareLinksAppFactory.MaxViewBytes * 20)));
        var response = await _client.PostAsync("/ShareLinks", content, Token);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(ShortLinkErrors.TooLarge, await response.ErrorCode());
    }

    [Fact]
    public async Task Create_BodyWithoutLengthUnderTheCap_IsSaved() {
        var content = new UnknownLengthContent(Encoding.UTF8.GetBytes(ShareLinkRequests.NewView()));
        var response = await _client.PostAsync("/ShareLinks", content, Token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // The rate limit is off in this app (CreatesPerHourPerAddress 0), so many links in a row are all made.
    [Fact]
    public async Task Create_WithTheLimitOff_IsNeverRateLimited() {
        for (int i = 0; i < RateLimitedShareLinksAppFactory.Limit + 5; i++)
            await Create(ShareLinkRequests.NewView());
    }

    // A body whose length the client doesn't say up front, as a chunked upload is sent.
    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length) {
            length = 0;
            return false;
        }
    }
}

// The limit on new links per address (#844): past it POST answers 429 with Retry-After, while opening links and every
// other endpoint carry on.
public class ShareLinks_RateLimit_Endpoint_Tests : IClassFixture<RateLimitedShareLinksAppFactory> {
    private readonly HttpClient _client;
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ShareLinks_RateLimit_Endpoint_Tests(RateLimitedShareLinksAppFactory factory) {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_PastTheLimit_Answers429WithRetryAfter_AndOtherEndpointsStillAnswer() {
        string? id = null;
        for (int i = 0; i < RateLimitedShareLinksAppFactory.Limit; i++) {
            var made = await _client.PostAsync("/ShareLinks", ShareLinkRequests.Json(ShareLinkRequests.NewView()), Token);
            Assert.Equal(HttpStatusCode.Created, made.StatusCode);
            id = (await made.ReadJson()).GetProperty("id").GetString();
        }

        var refused = await _client.PostAsync("/ShareLinks", ShareLinkRequests.Json(ShareLinkRequests.NewView()), Token);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        var retryAfter = refused.Headers.RetryAfter?.Delta;
        Assert.NotNull(retryAfter);
        Assert.InRange(retryAfter!.Value, TimeSpan.FromSeconds(1), TimeSpan.FromHours(1));
        var error = await refused.ReadJson();
        Assert.Equal(ShortLinkErrors.RateLimited, error.GetProperty("error").GetString());
        Assert.Equal(RateLimitedShareLinksAppFactory.Limit, error.GetProperty("createsPerHour").GetInt32());
        Assert.Equal((int)retryAfter.Value.TotalSeconds, error.GetProperty("retryAfterSeconds").GetInt32());

        // Opening links is not limited, and neither is anything else.
        for (int i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/ShareLinks/{id}", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/Navigation/ComplexityClasses", Token)).StatusCode);
    }
}

// Without a database the share endpoints answer 503 short_links_unavailable, and the rest of the API is untouched (#844).
public class ShareLinks_Unavailable_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly AppFactory _factory;
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ShareLinks_Unavailable_Endpoint_Tests(AppFactory factory) {
        _factory = factory;
    }

    private static async Task AssertUnavailable(HttpClient client) {
        var created = await client.PostAsync("/ShareLinks", ShareLinkRequests.Json(ShareLinkRequests.NewView()), Token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, created.StatusCode);
        var error = await created.ReadJson();
        Assert.Equal(ShortLinkErrors.Unavailable, error.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("message").GetString()));

        var opened = await client.GetAsync("/ShareLinks/AAAAAAAAAA", Token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, opened.StatusCode);
        Assert.Equal(ShortLinkErrors.Unavailable, await opened.ErrorCode());
    }

    [Fact]
    public async Task NoConnectionString_Answers503_ForCreateAndGet() {
        Assert.IsType<UnavailableShortLinkStore>(_factory.Services.GetRequiredService<IShortLinkStore>());
        await AssertUnavailable(_factory.CreateClient());
    }

    [Fact]
    public async Task NoConnectionString_OtherEndpointsStillAnswer() {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Navigation/ComplexityClasses", Token)).StatusCode);
    }

    // A database that is set up but can't be reached answers the same 503 (and the error is logged), not a 500.
    [Fact]
    public async Task UnreachableDatabase_Answers503_ForCreateAndGet() {
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> {
                [$"ConnectionStrings:{ShortLinkSettings.ConnectionStringName}"] = "Host=127.0.0.1;Port=1;Username=nobody;Password=none;Timeout=3",
            })));
        Assert.IsType<PostgresShortLinkStore>(factory.Services.GetRequiredService<IShortLinkStore>());
        await AssertUnavailable(factory.CreateClient());
    }

    [Fact]
    public async Task StoreThatFails_Answers503() {
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => {
            services.RemoveAll<IShortLinkStore>();
            services.AddSingleton<IShortLinkStore>(new FailingShortLinkStore());
        }));
        await AssertUnavailable(factory.CreateClient());
    }
}
