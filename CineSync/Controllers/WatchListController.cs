using CineSync.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CineSync.Controllers
{
    [Authorize]
    public class WatchListController : Controller
    {
        private readonly IRecommendationService _recommendationService;

        public WatchListController(IRecommendationService recommendationService)
        {
            _recommendationService = recommendationService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var items = await _recommendationService.GetWatchListItemsAsync(userId);

            return View(items);
        }

        [HttpPost]
        public async Task<IActionResult> MarkWatched(int movieId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _recommendationService.MarkAsWatchedAsync(userId, movieId);

            TempData["Success"] = "Movie marked as watched.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> MarkToWatch(int movieId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _recommendationService.MarkAsToWatchAsync(userId, movieId);

            TempData["Success"] = "Movie moved back to To Watch.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Remove(int movieId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _recommendationService.RemoveFromWatchListAsync(userId, movieId);

            TempData["Success"] = "Movie removed from watchlist.";
            return RedirectToAction("Index");
        }
    }
}
