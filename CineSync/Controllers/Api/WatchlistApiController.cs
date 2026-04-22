using CineSync.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CineSync.Controllers.Api
{
    [ApiController]
    [Route("api/watchlist")]
    [Authorize(AuthenticationSchemes = "Mobile")]
    public class WatchlistApiController : ControllerBase
    {
        private readonly IRecommendationService _recommendationService;

        public WatchlistApiController(IRecommendationService recommendationService)
        {
            _recommendationService = recommendationService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var items = await _recommendationService.GetWatchListItemsAsync(userId);

            return Ok(items
                .Select(item => new WatchlistItemResponse
                {
                    MovieId = item.MovieId,
                    Title = item.Movie?.Title ?? string.Empty,
                    Year = item.Movie?.Year ?? 0,
                    PosterPath = item.Movie?.PosterPath,
                    CategoryName = item.Movie?.Category?.Name,
                    State = item.IsWatched ? "Watched" : "ToWatch"
                })
                .ToList());
        }
    }
}
