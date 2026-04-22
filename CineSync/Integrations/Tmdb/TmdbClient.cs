using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace CineSync.Integrations.Tmdb
{
    public class TmdbClient : ITmdbClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly TmdbOptions _options;

        public TmdbClient(HttpClient httpClient, IOptions<TmdbOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;

            if (_httpClient.BaseAddress == null)
                _httpClient.BaseAddress = new Uri("https://api.themoviedb.org/3/");
        }

        public async Task<int?> GetNetflixProviderIdAsync(CancellationToken cancellationToken = default)
        {
            var response = await GetAsync<TmdbProviderListResponse>(
                $"watch/providers/movie?language={Uri.EscapeDataString(_options.Language)}",
                cancellationToken);

            return response?.Results
                .FirstOrDefault(provider => string.Equals(provider.ProviderName, "Netflix", StringComparison.OrdinalIgnoreCase))
                ?.ProviderId;
        }

        public async Task<IReadOnlyList<int>> DiscoverMovieIdsByProviderAsync(int providerId, int maxPages, CancellationToken cancellationToken = default)
        {
            maxPages = Math.Clamp(maxPages, 1, 25);

            var discoveredIds = new List<int>();
            var totalPages = 1;

            for (var page = 1; page <= maxPages && page <= totalPages; page++)
            {
                var response = await GetAsync<TmdbDiscoverMovieResponse>(
                    BuildDiscoverMovieQuery(providerId, page),
                    cancellationToken);

                if (response == null)
                    continue;

                totalPages = Math.Max(1, response.TotalPages);
                discoveredIds.AddRange(response.Results.Select(movie => movie.Id));
            }

            return discoveredIds
                .Distinct()
                .ToList();
        }

        public Task<TmdbMovieDetails?> GetMovieDetailsAsync(int tmdbMovieId, CancellationToken cancellationToken = default)
        {
            return GetAsync<TmdbMovieDetails>(
                $"movie/{tmdbMovieId}?language={Uri.EscapeDataString(_options.Language)}",
                cancellationToken);
        }

        public Task<TmdbMovieCredits?> GetMovieCreditsAsync(int tmdbMovieId, CancellationToken cancellationToken = default)
        {
            return GetAsync<TmdbMovieCredits>(
                $"movie/{tmdbMovieId}/credits?language={Uri.EscapeDataString(_options.Language)}",
                cancellationToken);
        }

        private string BuildDiscoverMovieQuery(int providerId, int page)
        {
            return "discover/movie"
                + $"?include_adult=false"
                + $"&include_video=false"
                + $"&language={Uri.EscapeDataString(_options.Language)}"
                + $"&page={page}"
                + $"&sort_by=popularity.desc"
                + $"&watch_region={Uri.EscapeDataString(_options.WatchRegion)}"
                + $"&with_watch_monetization_types=flatrate"
                + $"&with_watch_providers={providerId}";
        }

        private async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
        {
            EnsureConfigured();

            using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BearerToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return default;

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"TMDb request failed with status {(int)response.StatusCode} for '{relativeUrl}'. {responseBody}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        }

        private void EnsureConfigured()
        {
            if (!string.IsNullOrWhiteSpace(_options.BearerToken))
                return;

            throw new InvalidOperationException(
                "TMDb BearerToken is missing. Set it in appsettings.Development.json or user secrets under 'Tmdb:BearerToken'.");
        }

        private sealed class TmdbProviderListResponse
        {
            [JsonPropertyName("results")]
            public List<TmdbProviderItem> Results { get; set; } = new();
        }

        private sealed class TmdbProviderItem
        {
            [JsonPropertyName("provider_id")]
            public int ProviderId { get; set; }

            [JsonPropertyName("provider_name")]
            public string ProviderName { get; set; } = string.Empty;
        }

        private sealed class TmdbDiscoverMovieResponse
        {
            [JsonPropertyName("total_pages")]
            public int TotalPages { get; set; }

            [JsonPropertyName("results")]
            public List<TmdbDiscoverMovieItem> Results { get; set; } = new();
        }

        private sealed class TmdbDiscoverMovieItem
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }
        }
    }
}
