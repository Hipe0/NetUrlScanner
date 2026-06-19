using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NetURLScanner.Options;

namespace NetURLScanner.Services;

public class SafeBrowsingResult
{
    public string Status { get; set; } = "Disabled";
    public string? ThreatType { get; set; }
    public string? PlatformType { get; set; }
}

public class GoogleSafeBrowsingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GoogleSafeBrowsingOptions _options;
    private readonly ILogger<GoogleSafeBrowsingService> _logger;

    public GoogleSafeBrowsingService(
        IHttpClientFactory httpClientFactory,
        IOptions<GoogleSafeBrowsingOptions> options,
        ILogger<GoogleSafeBrowsingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SafeBrowsingResult> CheckUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
            return new SafeBrowsingResult { Status = "Disabled" };

        try
        {
            var client = _httpClientFactory.CreateClient("SafeBrowsing");
            var requestUrl = $"https://safebrowsing.googleapis.com/v4/threatMatches:find?key={Uri.EscapeDataString(_options.ApiKey)}";

            var payload = new
            {
                client = new { clientId = "neturlscanner", clientVersion = "1.0.0" },
                threatInfo = new
                {
                    threatTypes = new[]
                    {
                        "MALWARE", "SOCIAL_ENGINEERING", "UNWANTED_SOFTWARE",
                        "POTENTIALLY_HARMFUL_APPLICATION"
                    },
                    platformTypes = new[] { "ANY_PLATFORM" },
                    threatEntryTypes = new[] { "URL" },
                    threatEntries = new[] { new { url } }
                }
            };

            using var response = await client.PostAsJsonAsync(requestUrl, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Safe Browsing API HTTP {Status}", response.StatusCode);
                return new SafeBrowsingResult { Status = "Unavailable" };
            }

            var result = await response.Content.ReadFromJsonAsync<SafeBrowsingApiResponse>(cancellationToken: cancellationToken);
            var match = result?.Matches?.FirstOrDefault();
            if (match == null)
                return new SafeBrowsingResult { Status = "Clean" };

            return new SafeBrowsingResult
            {
                Status = "Threat",
                ThreatType = match.ThreatType,
                PlatformType = match.PlatformType
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Safe Browsing check failed");
            return new SafeBrowsingResult { Status = "Unavailable" };
        }
    }

    private sealed class SafeBrowsingApiResponse
    {
        [JsonPropertyName("matches")]
        public List<SafeBrowsingMatch>? Matches { get; set; }
    }

    private sealed class SafeBrowsingMatch
    {
        [JsonPropertyName("threatType")]
        public string? ThreatType { get; set; }

        [JsonPropertyName("platformType")]
        public string? PlatformType { get; set; }
    }
}
