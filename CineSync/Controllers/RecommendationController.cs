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
    }
}
