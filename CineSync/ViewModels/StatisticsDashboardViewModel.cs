namespace CineSync.ViewModels
{
    public class StatisticsDashboardViewModel
    {
        public int TotalMovies { get; set; }
        public int TotalUsers { get; set; }
        public int TotalReviews { get; set; }
        public int TotalSubscriptions { get; set; }

        public int TotalWatchlistItems { get; set; }
        public int TotalWatchedItems { get; set; }
        public double WatchedCompletionRate { get; set; }

        public int TotalLikes { get; set; }
        public int TotalDislikes { get; set; }
        public double LikeRatio { get; set; }

        public double AverageMovieRating { get; set; }
        public decimal AverageSubscriptionPrice { get; set; }

        public int ActiveCarts { get; set; }
        public int CartItemsCount { get; set; }

        public int ReviewsLast30Days { get; set; }
        public int ReactionsLast30Days { get; set; }
        public int WatchlistAddsLast30Days { get; set; }

        public List<MovieStatItemViewModel> TopRatedMovies { get; set; } = new();
        public List<MovieStatItemViewModel> MostReviewedMovies { get; set; } = new();
        public List<CategoryStatItemViewModel> TopCategories { get; set; } = new();
    }

    public class MovieStatItemViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string? Category { get; set; }
        public int ReviewsCount { get; set; }
        public double AverageRating { get; set; }
    }

    public class CategoryStatItemViewModel
    {
        public string CategoryName { get; set; } = string.Empty;
        public int MoviesCount { get; set; }
    }
}
