using CineSync.Models;
using CineSync.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CineSync.Controllers.Api
{
    [ApiController]
    [Route("api/movies")]
    [Authorize(AuthenticationSchemes = "Mobile")]
    public class MoviesApiController : ControllerBase
    {
        private readonly IMovieService _movieService;
        private readonly IRecommendationService _recommendationService;

        public MoviesApiController(
            IMovieService movieService,
            IRecommendationService recommendationService)
        {
            _movieService = movieService;
            _recommendationService = recommendationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMovies(int? categoryId = null, string? search = null, int page = 1, int pageSize = 20)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 50);

            List<Movie> movies;
            int totalCount;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var results = await _movieService.SearchMoviesAsync(search);
                var filtered = results
                    .Select(result => result.Movie)
                    .Where(movie => !categoryId.HasValue || movie.CategoryId == categoryId.Value)
                    .GroupBy(movie => movie.MovieId)
                    .Select(group => group.First())
                    .ToList();

                totalCount = filtered.Count;
                movies = filtered
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
            }
            else
            {
                totalCount = await _movieService.GetMoviesCountAsync(categoryId);
                movies = (await _movieService.GetMoviesAsync(categoryId, page, pageSize)).ToList();
            }

            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

            return Ok(new MovieListResponse
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                Items = movies.Select(MobileApiMapper.ToMovieListItem).ToList()
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetMovie(int id)
        {
            var movie = await _movieService.GetMovieByIdAsync(id);
            if (movie == null)
            {
                return NotFound(new { error = "Movie not found." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            return Ok(new MovieDetailsResponse
            {
                MovieId = movie.MovieId,
                Title = movie.Title,
                Year = movie.Year,
                Description = movie.Description,
                PosterPath = movie.PosterPath,
                PdfPath = movie.PdfPath,
                CategoryName = movie.Category?.Name,
                DirectorName = movie.Director?.Name,
                AverageRating = movie.Reviews != null && movie.Reviews.Any()
                    ? Math.Round(movie.Reviews.Average(review => review.Rating), 1)
                    : (double?)null,
                ReviewCount = movie.Reviews?.Count ?? 0,
                IsInWatchList = await _recommendationService.IsMovieInWatchListAsync(userId, id),
                IsWatched = await _recommendationService.IsMovieWatchedAsync(userId, id),
                UserReaction = (await _recommendationService.GetReactionAsync(userId, id))?.IsLiked,
                Cast = movie.Casts
                    .Where(cast => cast.Actor != null)
                    .Select(cast => new MovieCastResponse
                    {
                        ActorId = cast.ActorId,
                        ActorName = cast.Actor!.Name,
                        CharacterName = cast.CharacterName,
                        RoleType = cast.RoleType
                    })
                    .ToList(),
                Reviews = movie.Reviews
                    .OrderByDescending(review => review.Date)
                    .Select(MobileApiMapper.ToMovieReviewResponse)
                    .ToList(),
                SimilarMovies = (await _movieService.GetSimilarMoviesAsync(id, 4))
                    .Select(MobileApiMapper.ToSimilarMovieResponse)
                    .ToList()
            });
        }

        [HttpGet("{id:int}/reviews")]
        public async Task<IActionResult> GetMovieReviews(int id)
        {
            var movie = await _movieService.GetMovieByIdAsync(id);
            if (movie == null)
            {
                return NotFound(new { error = "Movie not found." });
            }

            return Ok(movie.Reviews
                .OrderByDescending(review => review.Date)
                .Select(MobileApiMapper.ToMovieReviewResponse)
                .ToList());
        }

        [HttpGet("/api/categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _movieService.GetAllCategoriesAsync();

            return Ok(categories
                .Select(category => new CategoryResponse
                {
                    CategoryId = category.CategoryId,
                    Name = category.Name
                })
                .ToList());
        }
    }
}
