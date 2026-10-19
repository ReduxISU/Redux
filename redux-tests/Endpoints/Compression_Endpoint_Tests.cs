using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Response compression (#909): a client that sends Accept-Encoding gets the same JSON, compressed; one that does not
// gets it plain. The test client does not decompress, so these tests see the bytes on the wire.
public class Compression_Endpoint_Tests : IClassFixture<AppFactory> {
    private const string Url = "/ProblemProvider/info?interface=SAT3";
    private readonly HttpClient _client;

    public Compression_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private async Task<HttpResponseMessage> Get(string? acceptEncoding) {
        var request = new HttpRequestMessage(HttpMethod.Get, Url);
        if (acceptEncoding != null)
            request.Headers.TryAddWithoutValidation("Accept-Encoding", acceptEncoding);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response;
    }

    private static string Decode(byte[] body, string encoding) {
        using Stream inner = new MemoryStream(body);
        using Stream stream = encoding switch {
            "br" => new BrotliStream(inner, CompressionMode.Decompress),
            "gzip" => new GZipStream(inner, CompressionMode.Decompress),
            _ => throw new ArgumentException(encoding),
        };
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private async Task<string> Plain() {
        var response = await Get(null);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("br", "br")]
    [InlineData("gzip", "gzip")]
    [InlineData("br, gzip", "br")]
    [InlineData("gzip, deflate", "gzip")]
    public async Task AcceptEncoding_GetsTheSameJsonCompressed(string acceptEncoding, string expected) {
        string plain = await Plain();
        var response = await Get(acceptEncoding);
        Assert.Equal([expected], response.Content.Headers.ContentEncoding);
        Assert.Contains("Accept-Encoding", response.Headers.Vary);
        byte[] body = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.True(body.Length < Encoding.UTF8.GetByteCount(plain), $"{expected}: {body.Length} bytes is not smaller than the plain body");
        string decoded = Decode(body, expected);
        Assert.Equal(plain, decoded);
        using var _ = JsonDocument.Parse(decoded);
    }

    [Fact]
    public async Task NoAcceptEncoding_GetsPlainJson() {
        var response = await Get(null);
        Assert.Empty(response.Content.Headers.ContentEncoding);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.ToString());
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("3SAT", doc.RootElement.GetProperty("problemName").GetString());
    }

    [Fact]
    public async Task UnknownEncoding_GetsPlainJson() {
        var response = await Get("compress");
        Assert.Empty(response.Content.Headers.ContentEncoding);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(await Plain(), body);
    }
}
