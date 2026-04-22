namespace CineSync.Integrations.Tmdb
{
    public interface ITmdbClient
    {
        Task<int?> GetNetflixProviderIdAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<int>> DiscoverMovieIdsByProviderAsync(int providerId, int maxPages, CancellationToken cancellationToken = default);
        Task<TmdbMovieDetails?> GetMovieDetailsAsync(int tmdbMovieId, CancellationToken cancellationToken = default);
        Task<TmdbMovieCredits?> GetMovieCreditsAsync(int tmdbMovieId, CancellationToken cancellationToken = default);
    }
}
