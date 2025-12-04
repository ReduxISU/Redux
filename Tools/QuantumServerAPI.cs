using System.Text;
using System.Text.Json;

namespace API.Tools;

/// <summary>
/// Client for making HTTP requests to external API servers.
/// Supports switching between different server environments.
/// </summary>
public class QuantumServerAPI
{
    // --- Server Configuration ---
    public enum ServerEnvironment
    {
        ISU_AWS,
        LOCAL
    }

    private static readonly Dictionary<ServerEnvironment, string> ServerUrls = new()
    {
        { ServerEnvironment.ISU_AWS, "http://towel.aws.cose.isu.edu:8080" },
        { ServerEnvironment.LOCAL, "http://127.0.0.1:5000" }
    };

    // --- Fields ---
    private readonly HttpClient _httpClient;
    private readonly ServerEnvironment _environment;
    private readonly string _baseUrl;

    // --- Constructor ---
    
    /// <summary>
    /// Creates a new QuantumServerAPI instance.
    /// </summary>
    /// <param name="environment">The server environment to use (ISU_AWS or LOCAL)</param>
    public QuantumServerAPI(ServerEnvironment environment = ServerEnvironment.ISU_AWS)
    {
        _environment = environment;
        _baseUrl = ServerUrls[environment];
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Creates a new QuantumServerAPI with a custom base URL.
    /// </summary>
    /// <param name="customBaseUrl">Custom base URL for the API server</param>
    public QuantumServerAPI(string customBaseUrl)
    {
        _baseUrl = customBaseUrl;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    // --- Public Methods ---

    /// <summary>
    /// Makes a POST request to the specified endpoint with a JSON body.
    /// </summary>
    /// <param name="endpoint">The API endpoint (e.g., "/deutsch")</param>
    /// <param name="body">The request body object (will be serialized to JSON)</param>
    /// <returns>The JSON response as a string</returns>
    /// <exception cref="HttpRequestException">Thrown when the request fails</exception>
    public async Task<string> PostAsync(string endpoint, object body)
    {
        try
        {
            // Serialize the body to JSON
            string jsonBody = JsonSerializer.Serialize(body);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            // Make the POST request
            HttpResponseMessage response = await _httpClient.PostAsync(endpoint, content);

            // Ensure success status code
            response.EnsureSuccessStatusCode();

            // Read and return the response
            string responseBody = await response.Content.ReadAsStringAsync();
            return responseBody;
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"Failed to POST to {_baseUrl}{endpoint}: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new TimeoutException($"Request to {_baseUrl}{endpoint} timed out after 30 seconds", ex);
        }
    }

    /// <summary>
    /// Makes a POST request and deserializes the response to the specified type.
    /// </summary>
    /// <typeparam name="TResponse">The type to deserialize the response to</typeparam>
    /// <param name="endpoint">The API endpoint (e.g., "/deutsch")</param>
    /// <param name="body">The request body object (will be serialized to JSON)</param>
    /// <returns>The deserialized response object</returns>
    public async Task<TResponse?> PostAsync<TResponse>(string endpoint, object body)
    {
        string responseJson = await PostAsync(endpoint, body);
        return JsonSerializer.Deserialize<TResponse>(responseJson);
    }

    /// <summary>
    /// Makes a POST request with a raw JSON string body.
    /// </summary>
    /// <param name="endpoint">The API endpoint</param>
    /// <param name="jsonBody">The raw JSON string to send</param>
    /// <returns>The JSON response as a string</returns>
    public async Task<string> PostRawJsonAsync(string endpoint, string jsonBody)
    {
        try
        {
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync(endpoint, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"Failed to POST to {_baseUrl}{endpoint}: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new TimeoutException($"Request to {_baseUrl}{endpoint} timed out after 30 seconds", ex);
        }
    }

    /// <summary>
    /// Gets the current base URL being used.
    /// </summary>
    public string GetBaseUrl() => _baseUrl;

    /// <summary>
    /// Gets the current environment (if applicable).
    /// </summary>
    public ServerEnvironment? GetEnvironment() => _environment;

    // --- Dispose ---
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
