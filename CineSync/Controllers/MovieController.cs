using CineSync.Models;
using CineSync.Services;
using CineSync.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CineSync.Controllers
{
    public class MovieController : Controller
    {
        private readonly IMovieService _movieService;
        private readonly IPdfService _pdfService;
        private readonly IRecommendationService _recommendationService;
        private readonly ITmdbImportService _tmdbImportService;

        public MovieController(
            IMovieService movieService,
            IPdfService pdfService,
            IRecommendationService recommendationService,
            ITmdbImportService tmdbImportService)
        {
            _movieService = movieService;
            _pdfService = pdfService;
            _recommendationService = recommendationService;
            _tmdbImportService = tmdbImportService;
        }

        public async Task<IActionResult> Index(int? categoryId, string? search, int page = 1)
        {
            const int pageSize = 9;
            page = Math.Max(page, 1);

            ViewBag.Categories = await _movieService.GetAllCategoriesAsync();
            ViewBag.SelectedCategory = categoryId;
            ViewBag.Search = search;
            ViewBag.PageSize = pageSize;

            IEnumerable<Movie> movies;  
            int totalResults;
            int totalPages;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var results = await _movieService.SearchMoviesAsync(search);
                if (categoryId.HasValue)
                    results = results.Where(r => r.Movie.CategoryId == categoryId.Value);

                var filteredResults = results.ToList();
                ViewBag.SearchResults = filteredResults;
                totalResults = filteredResults.Count;
                totalPages = Math.Max(1, (int)Math.Ceiling(totalResults / (double)pageSize));
                if (page > totalPages)
                    page = totalPages;

                movies = filteredResults
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => r.Movie)
                    .ToList();
            }
            else
            {
                totalResults = await _movieService.GetMoviesCountAsync(categoryId);
                totalPages = Math.Max(1, (int)Math.Ceiling(totalResults / (double)pageSize));
                if (page > totalPages)
                    page = totalPages;

                movies = await _movieService.GetMoviesAsync(categoryId, page, pageSize);
            }

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalResults = totalResults;

            ViewBag.AverageRatings = movies.ToDictionary(
                m => m.MovieId,
                m => (m.Reviews != null && m.Reviews.Any())
                    ? Math.Round(m.Reviews.Average(r => r.Rating), 1)
                    : 0.0
            );

            return View(movies);
        }

        [HttpGet]
        public async Task<IActionResult> Suggestions(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return Json(new List<MovieSuggestionViewModel>());

            var suggestions = await _movieService.GetMovieSuggestionsAsync(term, 6);

            return Json(suggestions.Select(s => new
            {
                movieId = s.MovieId,
                title = s.Title,
                year = s.Year,
                categoryName = s.CategoryName,
                score = s.Score
            }));
        }

        public async Task<IActionResult> Details(int id)
        {
            var movie = await _movieService.GetMovieByIdAsync(id);
            if (movie == null) return NotFound();

            ViewBag.AverageRating = movie.Reviews != null && movie.Reviews.Any()
                ? Math.Round(movie.Reviews.Average(r => r.Rating), 1)
                : (double?)null;

            ViewBag.ReviewCount = movie.Reviews?.Count ?? 0;
            ViewBag.SimilarMovies = await _movieService.GetSimilarMoviesAsync(id, 4);

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                ViewBag.UserHasReviewed = await _movieService.UserHasReviewedAsync(id, userId);
                ViewBag.IsInWatchList = await _recommendationService.IsMovieInWatchListAsync(userId, id);
                ViewBag.IsWatched = await _recommendationService.IsMovieWatchedAsync(userId, id);
                ViewBag.UserReaction = (await _recommendationService.GetReactionAsync(userId, id))?.IsLiked;
            }
            else
            {
                ViewBag.UserHasReviewed = false;
                ViewBag.IsInWatchList = false;
                ViewBag.IsWatched = false;
                ViewBag.UserReaction = null;
            }

            return View(movie);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Like(int movieId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _recommendationService.SaveReactionAsync(userId, movieId, true);

            TempData["ReviewSuccess"] = "Movie liked.";
            return RedirectToAction("Details", new { id = movieId });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Dislike(int movieId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _recommendationService.SaveReactionAsync(userId, movieId, false);

            TempData["ReviewSuccess"] = "Movie disliked.";
            return RedirectToAction("Details", new { id = movieId });
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

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportTmdb(int maxPages = 6, bool generatePdfs = false, CancellationToken cancellationToken = default)
        {
            maxPages = Math.Clamp(maxPages, 1, 25);

            try
            {
                var result = await _tmdbImportService.ImportNetflixCatalogAsync(maxPages, generatePdfs, cancellationToken);

                TempData["Success"] =
                    $"TMDb import finished for RO. Discovered: {result.DiscoveredMovieCount}, Created: {result.CreatedCount}, Updated: {result.UpdatedCount}, Skipped: {result.SkippedCount}, Failed: {result.FailedCount}.";
            }
            catch (Exception ex)
            {
                TempData["ImportError"] = ex.Message;
            }

            return RedirectToAction(nameof(Manage));
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

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _movieService.DeleteMovieAsync(id);
            return RedirectToAction(nameof(Manage));
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
