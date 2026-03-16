using CineSync.Models;

namespace CineSync.ViewModels
{
    public class RecommendationViewModel
    {
        public Movie? Movie { get; set; }

        public double? AverageRating { get; set; }
        public int ReviewCount { get; set; }

        public string Reason { get; set; } = "Recommended for you";

        public int CurrentIndex { get; set; }
        public int TotalRecommendations { get; set; }

        public bool IsInWatchList { get; set; }
        public bool IsWatched { get; set; }

        public bool? UserReaction { get; set; }
    }
}
