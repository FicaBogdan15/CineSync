using CineSync.Data;
using CineSync.Models;
using Microsoft.EntityFrameworkCore;

namespace CineSync.Services
{
    public class RecommendationService : IRecommendationService
    {
        private readonly ApplicationDbContext _context;

        public RecommendationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<WatchList> GetOrCreateWatchListAsync(string userId)
        {
            var watchList = await _context.WatchLists
                .Include(w => w.Items)
                    .ThenInclude(i => i.Movie)
                .FirstOrDefaultAsync(w => w.UserId == userId);

            if (watchList != null)
                return watchList;

            watchList = new WatchList
            {
                UserId = userId,
                Items = new List<WatchListItem>()
            };

            _context.WatchLists.Add(watchList);
            await _context.SaveChangesAsync();

            return watchList;
        }

        public async Task<List<WatchListItem>> GetWatchListItemsAsync(string userId)
        {
            var watchList = await GetOrCreateWatchListAsync(userId);

            return await _context.WatchListItems
                .Include(i => i.Movie)
                    .ThenInclude(m => m!.Category)
                .Include(i => i.Movie)
                    .ThenInclude(m => m!.Director)
                .Where(i => i.WatchListId == watchList.WatchListId)
                .OrderByDescending(i => i.AddedAt)
                .ToListAsync();
        }

        public async Task AddToWatchListAsync(string userId, int movieId)
        {
            var watchList = await GetOrCreateWatchListAsync(userId);

            var existing = await _context.WatchListItems
                .FirstOrDefaultAsync(i => i.WatchListId == watchList.WatchListId && i.MovieId == movieId);

            if (existing != null)
                return;

            _context.WatchListItems.Add(new WatchListItem
            {
                WatchListId = watchList.WatchListId,
                MovieId = movieId,
                IsWatched = false,
                AddedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
        }

        public async Task RemoveFromWatchListAsync(string userId, int movieId)
        {
            var watchList = await GetOrCreateWatchListAsync(userId);

            var existing = await _context.WatchListItems
                .FirstOrDefaultAsync(i => i.WatchListId == watchList.WatchListId && i.MovieId == movieId);

            if (existing == null)
                return;

            _context.WatchListItems.Remove(existing);
            await _context.SaveChangesAsync();
        }

        public async Task MarkAsWatchedAsync(string userId, int movieId)
        {
            var watchList = await GetOrCreateWatchListAsync(userId);

            var existing = await _context.WatchListItems
                .FirstOrDefaultAsync(i => i.WatchListId == watchList.WatchListId && i.MovieId == movieId);

            if (existing == null)
            {
                existing = new WatchListItem
                {
                    WatchListId = watchList.WatchListId,
                    MovieId = movieId,
                    IsWatched = true,
                    AddedAt = DateTime.Now,
                    WatchedAt = DateTime.Now
                };

                _context.WatchListItems.Add(existing);
            }
            else
            {
                existing.IsWatched = true;
                existing.WatchedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }

        public async Task MarkAsToWatchAsync(string userId, int movieId)
        {
            var watchList = await GetOrCreateWatchListAsync(userId);

            var existing = await _context.WatchListItems
                .FirstOrDefaultAsync(i => i.WatchListId == watchList.WatchListId && i.MovieId == movieId);

            if (existing == null)
            {
                _context.WatchListItems.Add(new WatchListItem
                {
                    WatchListId = watchList.WatchListId,
                    MovieId = movieId,
                    IsWatched = false,
                    AddedAt = DateTime.Now,
                    WatchedAt = null
                });
            }
            else
            {
                existing.IsWatched = false;
                existing.WatchedAt = null;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<bool> IsMovieInWatchListAsync(string userId, int movieId)
        {
            var watchList = await GetOrCreateWatchListAsync(userId);

            return await _context.WatchListItems
                .AnyAsync(i => i.WatchListId == watchList.WatchListId && i.MovieId == movieId);
        }

        public async Task<bool> IsMovieWatchedAsync(string userId, int movieId)
        {
            var watchList = await GetOrCreateWatchListAsync(userId);

            return await _context.WatchListItems
                .AnyAsync(i => i.WatchListId == watchList.WatchListId
                            && i.MovieId == movieId
                            && i.IsWatched);
        }

        public async Task SaveReactionAsync(string userId, int movieId, bool isLiked)
        {
            var existing = await _context.MovieReactions
                .FirstOrDefaultAsync(r => r.UserId == userId && r.MovieId == movieId);

            if (existing != null)
            {
                existing.IsLiked = isLiked;
                existing.Date = DateTime.Now;
            }
            else
            {
                _context.MovieReactions.Add(new MovieReaction
                {
                    UserId = userId,
                    MovieId = movieId,
                    IsLiked = isLiked,
                    Date = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task<MovieReaction?> GetReactionAsync(string userId, int movieId)
        {
            return await _context.MovieReactions
                .FirstOrDefaultAsync(r => r.UserId == userId && r.MovieId == movieId);
        }

        public async Task<List<Movie>> GetRecommendationQueueAsync(string userId, int count = 10)
        {
            var allMovies = await _context.Movies
                .Include(m => m.Category)
                .Include(m => m.Director)
                .Include(m => m.Reviews)
                .ToListAsync();

            if (!allMovies.Any())
                return new List<Movie>();

            var interactedMovieIds = await GetInteractedMovieIdsAsync(userId);

            var eligibleMovies = allMovies
                .Where(m => !interactedMovieIds.Contains(m.MovieId))
                .ToList();

            if (!eligibleMovies.Any())
                return new List<Movie>();

            var interactionProfile = await BuildUserPreferenceProfileAsync(userId);

            if (interactionProfile.TotalInteractions < 5)
            {
                return eligibleMovies
                    .OrderByDescending(GetGlobalMovieScore)
                    .Take(count)
                    .ToList();
            }

            var rankedMovies = eligibleMovies
                .Select(m => new
                {
                    Movie = m,
                    GlobalScore = GetGlobalMovieScore(m),
                    PreferenceScore = GetPreferenceScore(m, interactionProfile.CategoryScores),
                    FinalScore = GetGlobalMovieScore(m) + GetPreferenceScore(m, interactionProfile.CategoryScores)
                })
                .OrderByDescending(x => x.FinalScore)
                .ThenByDescending(x => x.GlobalScore)
                .Take(count)
                .Select(x => x.Movie)
                .ToList();

            return rankedMovies;
        }

        public async Task<Movie?> GetRecommendationByIndexAsync(string userId, int index = 0)
        {
            if (index < 0)
                index = 0;

            var queue = await GetRecommendationQueueAsync(userId, 10);

            if (!queue.Any())
                return null;

            if (index >= queue.Count)
                index = 0;

            return queue[index];
        }

        public async Task<double?> GetAverageRatingAsync(int movieId)
        {
            var ratings = await _context.Reviews
                .Where(r => r.MovieId == movieId)
                .Select(r => r.Rating)
                .ToListAsync();

            if (!ratings.Any())
                return null;

            return Math.Round(ratings.Average(), 1);
        }

        public async Task<int> GetReviewCountAsync(int movieId)
        {
            return await _context.Reviews.CountAsync(r => r.MovieId == movieId);
        }

        public async Task<string> GetRecommendationReasonAsync(string userId, Movie movie)
        {
            var likedReactions = await _context.MovieReactions
                .Include(r => r.Movie)
                    .ThenInclude(m => m!.Category)
                .Where(r => r.UserId == userId && r.IsLiked)
                .ToListAsync();

            var likedCategoryIds = likedReactions
                .Where(r => r.Movie != null)
                .Select(r => r.Movie!.CategoryId)
                .ToList();

            if (likedCategoryIds.Contains(movie.CategoryId))
                return $"Because you liked {movie.Category?.Name} movies";

            var reviewCount = await GetReviewCountAsync(movie.MovieId);
            if (reviewCount > 0)
                return "Popular among CineSync users";

            return "Recommended for you";
        }

        private async Task<HashSet<int>> GetInteractedMovieIdsAsync(string userId)
        {
            var reactionMovieIds = await _context.MovieReactions
                .Where(r => r.UserId == userId)
                .Select(r => r.MovieId)
                .ToListAsync();

            var reviewMovieIds = await _context.Reviews
                .Where(r => r.UserId == userId)
                .Select(r => r.MovieId)
                .ToListAsync();

            var watchList = await GetOrCreateWatchListAsync(userId);

            var watchListMovieIds = await _context.WatchListItems
                .Where(i => i.WatchListId == watchList.WatchListId)
                .Select(i => i.MovieId)
                .ToListAsync();

            return reactionMovieIds
                .Union(reviewMovieIds)
                .Union(watchListMovieIds)
                .ToHashSet();
        }

        private async Task<UserPreferenceProfile> BuildUserPreferenceProfileAsync(string userId)
        {
            var profile = new UserPreferenceProfile();

            var userReactions = await _context.MovieReactions
                .Include(r => r.Movie)
                    .ThenInclude(m => m!.Category)
                .Where(r => r.UserId == userId)
                .ToListAsync();

            var userReviews = await _context.Reviews
                .Include(r => r.Movie)
                    .ThenInclude(m => m!.Category)
                .Where(r => r.UserId == userId)
                .ToListAsync();

            var watchList = await GetOrCreateWatchListAsync(userId);

            var watchListItems = await _context.WatchListItems
                .Include(i => i.Movie)
                    .ThenInclude(m => m!.Category)
                .Where(i => i.WatchListId == watchList.WatchListId)
                .ToListAsync();

            foreach (var reaction in userReactions)
            {
                if (reaction.Movie == null)
                    continue;

                AddCategoryScore(profile.CategoryScores, reaction.Movie.CategoryId, reaction.IsLiked ? 3 : -4);
                profile.TotalInteractions++;
            }

            foreach (var review in userReviews)
            {
                if (review.Movie == null)
                    continue;

                int scoreToAdd;
                if (review.Rating >= 8)
                    scoreToAdd = 3;
                else if (review.Rating >= 5)
                    scoreToAdd = 1;
                else
                    scoreToAdd = -3;

                AddCategoryScore(profile.CategoryScores, review.Movie.CategoryId, scoreToAdd);
                profile.TotalInteractions++;
            }

            foreach (var item in watchListItems)
            {
                if (item.Movie == null)
                    continue;

                int scoreToAdd = item.IsWatched ? 1 : 2;
                AddCategoryScore(profile.CategoryScores, item.Movie.CategoryId, scoreToAdd);
                profile.TotalInteractions++;
            }

            return profile;
        }

        private static void AddCategoryScore(Dictionary<int, int> scores, int categoryId, int value)
        {
            if (!scores.ContainsKey(categoryId))
                scores[categoryId] = 0;

            scores[categoryId] += value;
        }

        private double GetPreferenceScore(Movie movie, Dictionary<int, int> categoryScores)
        {
            if (categoryScores.TryGetValue(movie.CategoryId, out int score))
                return score;

            return 0;
        }

        private double GetGlobalMovieScore(Movie movie)
        {
            var reviews = movie.Reviews?.ToList() ?? new List<Review>();

            if (!reviews.Any())
                return 0;

            double averageRating = reviews.Average(r => r.Rating);
            int reviewCount = reviews.Count;

            return averageRating * Math.Log(reviewCount + 1);
        }

        private class UserPreferenceProfile
        {
            public Dictionary<int, int> CategoryScores { get; set; } = new();
            public int TotalInteractions { get; set; }
        }
    }
}
