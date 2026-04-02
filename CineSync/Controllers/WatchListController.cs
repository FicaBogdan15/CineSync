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
        [Authorize]
        public async Task<IActionResult> MarkWatched(int movieId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _recommendationService.MarkAsWatchedAsync(userId, movieId);

            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) &&
                (referer.Contains("/Movie/Details") || referer.Contains("/WatchList")))
            {
                TempData["Success"] = "Marked as watched!";
                return Redirect(referer);
            }

            return RedirectToAction("Index", "Recommendation");
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> MarkToWatch(int movieId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _recommendationService.MarkAsToWatchAsync(userId, movieId);

            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) &&
                (referer.Contains("/Movie/Details") || referer.Contains("/WatchList")))
            {
                TempData["Success"] = "Moved to To-Watch!";
                return Redirect(referer);
            }

            return RedirectToAction("Index", "Recommendation");
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
