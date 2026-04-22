using CineSync.Models;
using CineSync.ViewModels;

namespace CineSync.Controllers.Api
{
    public sealed class ProfileResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int ReviewCount { get; set; }
        public int WatchlistCount { get; set; }
        public string SyncStatus { get; set; } = string.Empty;
        public List<string> ConnectedDevices { get; set; } = new();
    }

    public sealed class RecommendationResponse
    {
        public bool Empty { get; set; }
        public int MovieId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Year { get; set; }
        public string? Description { get; set; }
        public string? PosterPath { get; set; }
        public string? PdfPath { get; set; }
        public string? CategoryName { get; set; }
        public string? DirectorName { get; set; }
        public double? AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public string? Reason { get; set; }
        public int CurrentIndex { get; set; }
        public int TotalRecommendations { get; set; }
        public bool IsInWatchList { get; set; }
        public bool IsWatched { get; set; }
        public bool? UserReaction { get; set; }
    }

    public sealed class MovieListResponse
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public List<MovieListItemResponse> Items { get; set; } = new();
    }

    public class MovieListItemResponse
    {
        public int MovieId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Year { get; set; }
        public string? Description { get; set; }
        public string? PosterPath { get; set; }
        public string? PdfPath { get; set; }
        public string? CategoryName { get; set; }
        public double? AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }

    public sealed class MovieDetailsResponse : MovieListItemResponse
    {
        public string? DirectorName { get; set; }
        public bool IsInWatchList { get; set; }
        public bool IsWatched { get; set; }
        public bool? UserReaction { get; set; }
        public List<MovieCastResponse> Cast { get; set; } = new();
        public List<MovieReviewResponse> Reviews { get; set; } = new();
        public List<SimilarMovieResponse> SimilarMovies { get; set; } = new();
    }

    public sealed class MovieCastResponse
    {
        public int ActorId { get; set; }
        public string ActorName { get; set; } = string.Empty;
        public string? CharacterName { get; set; }
        public string? RoleType { get; set; }
    }

    public sealed class MovieReviewResponse
    {
        public int ReviewId { get; set; }
        public string Author { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string ReviewText { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }

    public sealed class SimilarMovieResponse
    {
        public int MovieId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public string? CategoryName { get; set; }
        public int Year { get; set; }
        public double SimilarityScore { get; set; }
    }

    public sealed class CategoryResponse
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class WatchlistItemResponse
    {
        public int MovieId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Year { get; set; }
        public string? PosterPath { get; set; }
        public string? CategoryName { get; set; }
        public string State { get; set; } = string.Empty;
    }

    public sealed class SyncStatusResponse
    {
        public bool HasActiveSession { get; set; }
        public int ActiveSessionCount { get; set; }
        public int PairedSessionCount { get; set; }
        public string SyncStatus { get; set; } = string.Empty;
        public List<string> ConnectedDevices { get; set; } = new();
    }

    public record RecommendationActionRequest(int MovieId, string Action);
    public record ReviewCreateRequest(int MovieId, int Rating, string ReviewText);
    public record SyncPairRequest(string Token, string? DeviceName);
    public record SyncActionRequest(string Token, string Action, int MovieId);

    public static class MobileApiMapper
    {
        public static MovieListItemResponse ToMovieListItem(Movie movie)
        {
            return new MovieListItemResponse
            {
                MovieId = movie.MovieId,
                Title = movie.Title,
                Year = movie.Year,
                Description = movie.Description,
                PosterPath = movie.PosterPath,
                PdfPath = movie.PdfPath,
                CategoryName = movie.Category?.Name,
                AverageRating = CalculateAverageRating(movie.Reviews),
                ReviewCount = movie.Reviews?.Count ?? 0
            };
        }

        public static MovieReviewResponse ToMovieReviewResponse(Review review)
        {
            return new MovieReviewResponse
            {
                ReviewId = review.ReviewId,
                Author = review.User?.UserName ?? review.User?.Email ?? "Anonymous",
                Rating = review.Rating,
                ReviewText = review.ReviewText,
                Date = review.Date
            };
        }

        public static SimilarMovieResponse ToSimilarMovieResponse(SimilarMovieViewModel movie)
        {
            return new SimilarMovieResponse
            {
                MovieId = movie.MovieId,
                Title = movie.Title ?? string.Empty,
                PosterPath = movie.PosterPath,
                CategoryName = movie.CategoryName,
                Year = movie.Year,
                SimilarityScore = movie.SimilarityScore
            };
        }

        public static string BuildSyncStatus(IReadOnlyCollection<PairingSession> sessions)
        {
            return sessions.Any(session => session.IsPaired) ? "PAIRED" : "READY TO PAIR";
        }

        public static List<string> BuildConnectedDevices(IReadOnlyCollection<PairingSession> sessions)
        {
            var devices = sessions
                .Select(session =>
                {
                    var deviceName = string.IsNullOrWhiteSpace(session.DeviceName)
                        ? "Desktop station"
                        : session.DeviceName!;

                    var status = session.IsPaired ? "active" : "waiting for pairing";
                    return $"{deviceName}: {status}";
                })
                .ToList();

            devices.Add("Mobile controller: active on this device");

            if (sessions.Count == 0)
            {
                devices.Insert(0, "Desktop station: offline");
            }

            return devices;
        }

        private static double? CalculateAverageRating(ICollection<Review>? reviews)
        {
            if (reviews == null || reviews.Count == 0)
            {
                return null;
            }

            return Math.Round(reviews.Average(review => review.Rating), 1);
        }
    }
}
