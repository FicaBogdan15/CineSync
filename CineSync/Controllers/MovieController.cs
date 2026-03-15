using CineSync.Models;
using CineSync.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CineSync.Controllers
{
    public class MovieController : Controller
    {
        private readonly IMovieService _movieService;
        private readonly IPdfService _pdfService;

        public MovieController(IMovieService movieService, IPdfService pdfService)
        {
            _movieService = movieService;
            _pdfService = pdfService;
        }

        public async Task<IActionResult> Index(int? categoryId, string? search)
        {
            ViewBag.Categories = await _movieService.GetAllCategoriesAsync();
            ViewBag.SelectedCategory = categoryId;
            ViewBag.Search = search;

            IEnumerable<Movie> movies;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var results = await _movieService.SearchMoviesAsync(search);
                if (categoryId.HasValue)
                    results = results.Where(r => r.Movie.CategoryId == categoryId.Value);

                ViewBag.SearchResults = results.ToList();
                movies = results.Select(r => r.Movie).ToList();
            }
            else
            {
                movies = await _movieService.GetMoviesAsync(categoryId);
            }

            ViewBag.AverageRatings = movies.ToDictionary(
                m => m.MovieId,
                m => (m.Reviews != null && m.Reviews.Any())
                    ? Math.Round(m.Reviews.Average(r => r.Rating), 1)
                    : 0.0
            );

            return View(movies);
        }

        public async Task<IActionResult> Details(int id)
        {
            var movie = await _movieService.GetMovieByIdAsync(id);
            if (movie == null) return NotFound();

            ViewBag.AverageRating = movie.Reviews != null && movie.Reviews.Any()
                ? Math.Round(movie.Reviews.Average(r => r.Rating), 1)
                : (double?)null;

            ViewBag.ReviewCount = movie.Reviews?.Count ?? 0;

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                ViewBag.UserHasReviewed = await _movieService.UserHasReviewedAsync(id, userId);
            }
            else
            {
                ViewBag.UserHasReviewed = false;
            }

            return View(movie);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddReview(int movieId, int rating, string reviewText)
        {
            var movie = await _movieService.GetMovieByIdAsync(movieId);
            if (movie == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var alreadyReviewed = await _movieService.UserHasReviewedAsync(movieId, userId);
            if (alreadyReviewed)
            {
                TempData["ReviewError"] = "You have already reviewed this movie.";
                return RedirectToAction("Details", new { id = movieId });
            }

            if (rating < 1 || rating > 10)
            {
                TempData["ReviewError"] = "Rating must be between 1 and 10.";
                return RedirectToAction("Details", new { id = movieId });
            }

            if (string.IsNullOrWhiteSpace(reviewText))
            {
                TempData["ReviewError"] = "Please write a comment.";
                return RedirectToAction("Details", new { id = movieId });
            }

            var review = new Review
            {
                MovieId = movieId,
                UserId = userId,
                Rating = rating,
                ReviewText = reviewText.Trim(),
                Date = DateTime.Now
            };

            await _movieService.AddReviewAsync(review);

            TempData["ReviewSuccess"] = "Review added successfully!";
            return RedirectToAction("Details", new { id = movieId });
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage()
        {
            ViewBag.Categories = await _movieService.GetAllCategoriesAsync();
            var movies = await _movieService.GetMoviesAsync(null);
            return View(movies);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _movieService.GetAllCategoriesAsync();
            ViewBag.Directors = await _movieService.GetAllDirectorsAsync();
            ViewBag.Actors = await _movieService.GetAllActorsAsync();
            return View(new Movie());
        }

        [HttpPost, Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Movie movie, List<int> selectedActors, List<string> characterNames, List<string> roleTypes)
        {
            CleanModelState();

            if (!ModelState.IsValid)
            {
                await PopulateViewBags();
                return View(movie);
            }

            await _movieService.AddMovieAsync(movie);

            var casts = BuildCastList(movie.MovieId, selectedActors, characterNames, roleTypes);
            await _movieService.AddCastsAsync(casts);

            var createdMovie = await _movieService.GetMovieByIdAsync(movie.MovieId);
            if (createdMovie != null)
            {
                var pdfPath = _pdfService.GenerateMoviePdf(createdMovie);
                await _movieService.SetPdfPathAsync(movie.MovieId, pdfPath);
            }

            return RedirectToAction("Manage");
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var movie = await _movieService.GetMovieByIdAsync(id);
            if (movie == null) return NotFound();

            await PopulateViewBags();
            return View(movie);
        }

        [HttpPost, Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Movie movie, List<int> selectedActors, List<string> characterNames, List<string> roleTypes)
        {
            CleanModelState();

            if (!ModelState.IsValid)
            {
                await PopulateViewBags();
                return View(movie);
            }

            await _movieService.UpdateMovieAsync(movie);

            await _movieService.DeleteCastsForMovieAsync(movie.MovieId);

            var newCasts = BuildCastList(movie.MovieId, selectedActors, characterNames, roleTypes);
            await _movieService.AddCastsAsync(newCasts);

            var updated = await _movieService.GetMovieByIdAsync(movie.MovieId);
            if (updated != null)
            {
                var pdfPath = _pdfService.GenerateMoviePdf(updated);
                await _movieService.SetPdfPathAsync(movie.MovieId, pdfPath);
            }

            return RedirectToAction("Manage");
        }

        private void CleanModelState()
        {
            ModelState.Remove("Director");
            ModelState.Remove("Category");
            ModelState.Remove("Casts");
            ModelState.Remove("Reviews");
            ModelState.Remove("AvailableOnPlatforms");
        }

        private async Task PopulateViewBags()
        {
            ViewBag.Categories = await _movieService.GetAllCategoriesAsync();
            ViewBag.Directors = await _movieService.GetAllDirectorsAsync();
            ViewBag.Actors = await _movieService.GetAllActorsAsync();
        }

        private List<Cast> BuildCastList(int movieId, List<int> actors, List<string> names, List<string> roles)
        {
            var castList = new List<Cast>();
            if (actors == null) return castList;

            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] == 0) continue;

                castList.Add(new Cast
                {
                    MovieId = movieId,
                    ActorId = actors[i],
                    CharacterName = names.ElementAtOrDefault(i) ?? "",
                    RoleType = roles.ElementAtOrDefault(i) ?? "Support"
                });
            }

            return castList;
        }
    }
}