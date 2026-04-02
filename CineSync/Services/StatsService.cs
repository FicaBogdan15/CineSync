using CineSync.Data;
using CineSync.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CineSync.Services
{
    public class StatsService : IStatisticsService
    {
        private readonly ApplicationDbContext _context;

        public StatsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<StatisticsDashboardViewModel> GetDashboardAsync()
        {
            var now = DateTime.Now;
            var threshold30 = now.AddDays(-30);

            var totalMovies = await _context.Movies.CountAsync();
            var totalUsers = await _context.Users.CountAsync();
            var totalReviews = await _context.Reviews.CountAsync();
            var totalSubscriptions = await _context.Subscriptions.CountAsync();

            var totalWatchlistItems = await _context.WatchListItems.CountAsync();
            var totalWatchedItems = await _context.WatchListItems.CountAsync(w => w.IsWatched);

            var totalLikes = await _context.MovieReactions.CountAsync(r => r.IsLiked);
            var totalDislikes = await _context.MovieReactions.CountAsync(r => !r.IsLiked);

            var averageMovieRating = await _context.Reviews.AnyAsync()
                ? await _context.Reviews.AverageAsync(r => (double)r.Rating)
                : 0;

            var averageSubscriptionPrice = await _context.Subscriptions.AnyAsync()
                ? await _context.Subscriptions.AverageAsync(s => s.MonthlyPrice)
                : 0;

            var activeCarts = await _context.Carts.CountAsync(c => c.CartItems.Any());
            var cartItemsCount = await _context.CartItems.CountAsync();

            var reviewsLast30Days = await _context.Reviews.CountAsync(r => r.Date >= threshold30);
            var reactionsLast30Days = await _context.MovieReactions.CountAsync(r => r.Date >= threshold30);
            var watchlistAddsLast30Days = await _context.WatchListItems.CountAsync(w => w.AddedAt >= threshold30);

            var topRatedMovies = await _context.Movies
                .Include(m => m.Category)
                .Include(m => m.Reviews)
                .Where(m => m.Reviews.Any())
                .Select(m => new MovieStatItemViewModel
                {
                    Title = m.Title,
                    Category = m.Category != null ? m.Category.Name : null,
                    ReviewsCount = m.Reviews.Count,
                    AverageRating = m.Reviews.Average(r => r.Rating)
                })
                .OrderByDescending(m => m.AverageRating)
                .ThenByDescending(m => m.ReviewsCount)
                .Take(5)
                .ToListAsync();

            var mostReviewedMovies = await _context.Movies
                .Include(m => m.Category)
                .Include(m => m.Reviews)
                .Where(m => m.Reviews.Any())
                .Select(m => new MovieStatItemViewModel
                {
                    Title = m.Title,
                    Category = m.Category != null ? m.Category.Name : null,
                    ReviewsCount = m.Reviews.Count,
                    AverageRating = m.Reviews.Average(r => r.Rating)
                })
                .OrderByDescending(m => m.ReviewsCount)
                .ThenByDescending(m => m.AverageRating)
                .Take(5)
                .ToListAsync();

            var topCategories = await _context.Categories
                .Select(c => new CategoryStatItemViewModel
                {
                    CategoryName = c.Name,
                    MoviesCount = c.Movies.Count
                })
                .OrderByDescending(c => c.MoviesCount)
                .Take(6)
                .ToListAsync();

            return new StatisticsDashboardViewModel
            {
                TotalMovies = totalMovies,
                TotalUsers = totalUsers,
                TotalReviews = totalReviews,
                TotalSubscriptions = totalSubscriptions,

                TotalWatchlistItems = totalWatchlistItems,
                TotalWatchedItems = totalWatchedItems,
                WatchedCompletionRate = totalWatchlistItems == 0
                    ? 0
                    : Math.Round((double)totalWatchedItems / totalWatchlistItems * 100, 1),

                TotalLikes = totalLikes,
                TotalDislikes = totalDislikes,
                LikeRatio = (totalLikes + totalDislikes) == 0
                    ? 0
                    : Math.Round((double)totalLikes / (totalLikes + totalDislikes) * 100, 1),

                AverageMovieRating = Math.Round(averageMovieRating, 2),
                AverageSubscriptionPrice = Math.Round(averageSubscriptionPrice, 2),

                ActiveCarts = activeCarts,
                CartItemsCount = cartItemsCount,

                ReviewsLast30Days = reviewsLast30Days,
                ReactionsLast30Days = reactionsLast30Days,
                WatchlistAddsLast30Days = watchlistAddsLast30Days,

                TopRatedMovies = topRatedMovies,
                MostReviewedMovies = mostReviewedMovies,
                TopCategories = topCategories
            };
        }
    }
}
