using CineSync.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace CineSync.Controllers.Api
{
    [ApiController]
    [Route("api/recommendations")]
    [Authorize(AuthenticationSchemes = "Mobile")]
    public class RecommendationsApiController : ControllerBase
    {
        private readonly IRecommendationService _recommendationService;
        private readonly IMemoryCache _cache;

        public RecommendationsApiController(
            IRecommendationService recommendationService,
            IMemoryCache cache)
        {
            _recommendationService = recommendationService;
            _cache = cache;
        }

        [HttpGet]
        public async Task<IActionResult> Get(int index = 0)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var queue = await GetCachedRecommendationQueueAsync(userId);
            if (!queue.Any())
            {
                return Ok(new RecommendationResponse
                {
                    Empty = true,
                    CurrentIndex = 0,
                    TotalRecommendations = 0
                });
            }

            index = Math.Clamp(index, 0, queue.Count - 1);
            var movie = queue[index];

            var reason = await _recommendationService.GetRecommendationReasonAsync(userId, ToReasonMovie(movie));
            var isInWatchList = await _recommendationService.IsMovieInWatchListAsync(userId, movie.MovieId);
            var isWatched = await _recommendationService.IsMovieWatchedAsync(userId, movie.MovieId);
            var reaction = await _recommendationService.GetReactionAsync(userId, movie.MovieId);

            return Ok(new RecommendationResponse
            {
                Empty = false,
                MovieId = movie.MovieId,
                Title = movie.Title,
                Year = movie.Year,
                Description = movie.Description,
                PosterPath = movie.PosterPath,
                PdfPath = movie.PdfPath,
                CategoryName = movie.CategoryName,
                DirectorName = movie.DirectorName,
                AverageRating = movie.AverageRating,
                ReviewCount = movie.ReviewCount,
                Reason = reason,
                CurrentIndex = index,
                TotalRecommendations = queue.Count,
                IsInWatchList = isInWatchList,
                IsWatched = isWatched,
                UserReaction = reaction?.IsLiked
            });
        }

        [HttpPost("action")]
        public async Task<IActionResult> Action([FromBody] RecommendationActionRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            switch ((request.Action ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "like":
                    await _recommendationService.SaveReactionAsync(userId, request.MovieId, true);
                    break;
                case "dislike":
                    await _recommendationService.SaveReactionAsync(userId, request.MovieId, false);
                    break;
                case "watchlist":
                    await _recommendationService.AddToWatchListAsync(userId, request.MovieId);
                    break;
                case "unwatchlist":
                    await _recommendationService.RemoveFromWatchListAsync(userId, request.MovieId);
                    break;
                case "watched":
                    await _recommendationService.MarkAsWatchedAsync(userId, request.MovieId);
                    break;
                case "towatch":
                    await _recommendationService.MarkAsToWatchAsync(userId, request.MovieId);
                    break;
                default:
                    return BadRequest(new { error = "Unsupported recommendation action." });
            }

            InvalidateRecommendationCache(userId);
            return Ok(new { ok = true });
        }

        private async Task<List<CachedRecommendationMovie>> GetCachedRecommendationQueueAsync(string userId)
        {
            var cacheKey = GetRecommendationCacheKey(userId);

            if (_cache.TryGetValue(cacheKey, out List<CachedRecommendationMovie>? cachedQueue))
            {
                return cachedQueue ?? new List<CachedRecommendationMovie>();
            }

            var queue = await _recommendationService.GetRecommendationQueueAsync(userId, 20);
            var items = queue.Select(movie => new CachedRecommendationMovie
            {
                MovieId = movie.MovieId,
                Title = movie.Title,
                Year = movie.Year,
                Description = movie.Description,
                PosterPath = movie.PosterPath,
                PdfPath = movie.PdfPath,
                CategoryId = movie.CategoryId,
                CategoryName = movie.Category?.Name,
                DirectorName = movie.Director?.Name,
                AverageRating = movie.Reviews != null && movie.Reviews.Any()
                    ? Math.Round(movie.Reviews.Average(review => review.Rating), 1)
                    : (double?)null,
                ReviewCount = movie.Reviews?.Count ?? 0
            }).ToList();

            _cache.Set(
                cacheKey,
                items,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(45),
                    SlidingExpiration = TimeSpan.FromSeconds(20)
                });

            return items;
        }

        private static string GetRecommendationCacheKey(string userId)
        {
            return $"mobile_recommendations_queue_{userId}";
        }

        private void InvalidateRecommendationCache(string userId)
        {
            _cache.Remove(GetRecommendationCacheKey(userId));
        }

        private static global::CineSync.Models.Movie ToReasonMovie(CachedRecommendationMovie movie)
        {
            return new global::CineSync.Models.Movie
            {
                MovieId = movie.MovieId,
                CategoryId = movie.CategoryId,
                Category = string.IsNullOrWhiteSpace(movie.CategoryName)
                    ? null
                    : new global::CineSync.Models.Category { Name = movie.CategoryName }
            };
        }

        private sealed class CachedRecommendationMovie
        {
            public int MovieId { get; init; }
            public string Title { get; init; } = string.Empty;
            public int Year { get; init; }
            public string? Description { get; init; }
            public string? PosterPath { get; init; }
            public string? PdfPath { get; init; }
            public int CategoryId { get; init; }
            public string? CategoryName { get; init; }
            public string? DirectorName { get; init; }
            public double? AverageRating { get; init; }
            public int ReviewCount { get; init; }
        }
    }
}
