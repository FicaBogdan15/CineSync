using CineSync.Services;
using CineSync.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CineSync.Controllers
{
    [Authorize]
    public class RecommendationController : Controller
    {
        private readonly IRecommendationService _recommendationService;

        public RecommendationController(IRecommendationService recommendationService)
        {
            _recommendationService = recommendationService;
        }

        public async Task<IActionResult> Index(int index = 0)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var queue = await _recommendationService.GetRecommendationQueueAsync(userId, 10);

            if (!queue.Any())
            {
                TempData["Info"] = "No more recommendations available right now.";

                return View(new RecommendationViewModel
                {
                    Movie = null,
                    CurrentIndex = 0,
                    TotalRecommendations = 0
                });
            }

            if (index < 0)
                index = 0;
            if (index >= queue.Count)
                index = 0;

            var movie = queue[index];

            var avgRating = await _recommendationService.GetAverageRatingAsync(movie.MovieId);
            var reviewCount = await _recommendationService.GetReviewCountAsync(movie.MovieId);
            var reason = await _recommendationService.GetRecommendationReasonAsync(userId, movie);

            var isInWatchList = await _recommendationService.IsMovieInWatchListAsync(userId, movie.MovieId);
            var isWatched = await _recommendationService.IsMovieWatchedAsync(userId, movie.MovieId);
            var reaction = await _recommendationService.GetReactionAsync(userId, movie.MovieId);

            var vm = new RecommendationViewModel
            {
                Movie = movie,
                AverageRating = avgRating,
                ReviewCount = reviewCount,
                Reason = reason,
                CurrentIndex = index,
                TotalRecommendations = queue.Count,
                IsInWatchList = isInWatchList,
                IsWatched = isWatched,
                UserReaction = reaction == null ? null : reaction.IsLiked
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Like(int movieId, int currentIndex = 0)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _recommendationService.SaveReactionAsync(userId, movieId, true);

            TempData["Success"] = "Movie liked.";
            return RedirectToAction("Index", new { index = currentIndex });
        }

        [HttpPost]
        public async Task<IActionResult> Dislike(int movieId, int currentIndex = 0)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _recommendationService.SaveReactionAsync(userId, movieId, false);

            TempData["Success"] = "Movie disliked.";
            return RedirectToAction("Index", new { index = currentIndex });
        }

        [HttpPost]
        public async Task<IActionResult> AddToWatchList(int movieId, int currentIndex = 0)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _recommendationService.AddToWatchListAsync(userId, movieId);

            TempData["Success"] = "Movie added to watchlist.";
            return RedirectToAction("Index", new { index = currentIndex });
        }

        [HttpPost]
        public async Task<IActionResult> MarkWatched(int movieId, int currentIndex = 0)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _recommendationService.MarkAsWatchedAsync(userId, movieId);

            TempData["Success"] = "Movie marked as watched.";
            return RedirectToAction("Index", new { index = currentIndex });
        }

        [HttpPost]
        public async Task<IActionResult> MarkToWatch(int movieId, int currentIndex = 0)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _recommendationService.MarkAsToWatchAsync(userId, movieId);

            TempData["Success"] = "Movie marked as to watch.";
            return RedirectToAction("Index", new { index = currentIndex });
        }

        public IActionResult Next(int currentIndex = 0)
        {
            return RedirectToAction("Index", new { index = currentIndex + 1 });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetRecommendation(int currentIndex = 0)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var queue = await _recommendationService.GetRecommendationQueueAsync(userId, 20);
            var list = queue.ToList();

            if (!list.Any())
                return Json(new { empty = true });

            currentIndex = Math.Clamp(currentIndex, 0, list.Count - 1);
            var movie = list[currentIndex];

            var avgRating = movie.Reviews != null && movie.Reviews.Any()
                ? Math.Round(movie.Reviews.Average(r => r.Rating), 1)
                : (double?)null;

            var reaction = await _recommendationService.GetReactionAsync(userId, movie.MovieId);
            var isInWatchList = await _recommendationService.IsMovieInWatchListAsync(userId, movie.MovieId);
            var isWatched = await _recommendationService.IsMovieWatchedAsync(userId, movie.MovieId);

            return Json(new
            {
                empty = false,
                currentIndex,
                total = list.Count,
                movieId = movie.MovieId,
                title = movie.Title,
                year = movie.Year,
                description = movie.Description,
                posterPath = movie.PosterPath,
                pdfPath = movie.PdfPath,
                categoryName = movie.Category?.Name,
                directorName = movie.Director?.Name,
                avgRating,
                reviewCount = movie.Reviews?.Count ?? 0,
                userReaction = reaction?.IsLiked,
                isInWatchList,
                isWatched,
                reason = currentIndex < 5 ? "Top rated globally" : "Based on your preferences"
            });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ActionJson([FromBody] RecommendationActionRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            switch ((request.Action ?? string.Empty).ToLowerInvariant())
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
            }

            return Json(new { ok = true });
        }

        public class RecommendationActionRequest
        {
            public string? Action { get; set; }
            public int MovieId { get; set; }
            public int CurrentIndex { get; set; }
        }
    }
}
