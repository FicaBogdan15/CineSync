using CineSync.Models;

namespace CineSync.Services
{
    public interface IRecommendationService
    {
        Task<WatchList> GetOrCreateWatchListAsync(string userId);
        Task<List<WatchListItem>> GetWatchListItemsAsync(string userId);

        Task SaveReactionAsync(string userId, int movieId, bool isLiked);
        Task<MovieReaction?> GetReactionAsync(string userId, int movieId);

        Task AddToWatchListAsync(string userId, int movieId);
        Task RemoveFromWatchListAsync(string userId, int movieId);

        Task MarkAsWatchedAsync(string userId, int movieId);
        Task MarkAsToWatchAsync(string userId, int movieId);

        Task<bool> IsMovieInWatchListAsync(string userId, int movieId);
        Task<bool> IsMovieWatchedAsync(string userId, int movieId);

        Task<List<Movie>> GetRecommendationQueueAsync(string userId, int count = 10);
        Task<Movie?> GetRecommendationByIndexAsync(string userId, int index = 0);

        Task<double?> GetAverageRatingAsync(int movieId);
        Task<int> GetReviewCountAsync(int movieId);

        Task<string> GetRecommendationReasonAsync(string userId, Movie movie);
    }
}
