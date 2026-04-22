using CineSync.Models;
using CineSync.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CineSync.Controllers.Api
{
    [ApiController]
    [Route("api/reviews")]
    [Authorize(AuthenticationSchemes = "Mobile")]
    public class ReviewsApiController : ControllerBase
    {
        private readonly IMovieService _movieService;

        public ReviewsApiController(IMovieService movieService)
        {
            _movieService = movieService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ReviewCreateRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? "Anonymous";
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var movie = await _movieService.GetMovieByIdAsync(request.MovieId);
            if (movie == null)
            {
                return NotFound(new { error = "Movie not found." });
            }

            if (await _movieService.UserHasReviewedAsync(request.MovieId, userId))
            {
                return Conflict(new { error = "You have already reviewed this movie." });
            }

            if (request.Rating < 1 || request.Rating > 10)
            {
                return BadRequest(new { error = "Rating must be between 1 and 10." });
            }

            if (string.IsNullOrWhiteSpace(request.ReviewText))
            {
                return BadRequest(new { error = "Review text is required." });
            }

            var review = new Review
            {
                MovieId = request.MovieId,
                UserId = userId,
                Rating = request.Rating,
                ReviewText = request.ReviewText.Trim(),
                Date = DateTime.Now
            };

            await _movieService.AddReviewAsync(review);

            return Ok(new MovieReviewResponse
            {
                ReviewId = review.ReviewId,
                Author = userName,
                Rating = review.Rating,
                ReviewText = review.ReviewText,
                Date = review.Date
            });
        }
    }
}
