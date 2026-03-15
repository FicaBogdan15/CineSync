using CineSync.Data;
using CineSync.Models;
using Microsoft.EntityFrameworkCore;

namespace CineSync.Services
{
    public class MovieService : IMovieService
    {
        private readonly ApplicationDbContext _context;

        public MovieService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Movie>> GetMoviesAsync(int? categoryId)
        {
            var query = _context.Movies
                .Include(m => m.Category)
                .Include(m => m.Reviews)
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(m => m.CategoryId == categoryId.Value);

            return await query.ToListAsync();
        }

        public async Task<Movie?> GetMovieByIdAsync(int id)
        {
            return await _context.Movies
                .Include(m => m.Category)
                .Include(m => m.Director)
                .Include(m => m.Casts)!.ThenInclude(c => c.Actor)
                .Include(m => m.Reviews)!.ThenInclude(r => r.User)
                .FirstOrDefaultAsync(m => m.MovieId == id);
        }

        public async Task<IEnumerable<Category>> GetAllCategoriesAsync()
        {
            return await _context.Categories.ToListAsync();
        }

        public async Task AddMovieAsync(Movie movie)
        {
            _context.Movies.Add(movie);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateMovieAsync(Movie movie)
        {
            var existing = await _context.Movies.FindAsync(movie.MovieId);
            if (existing == null) return;

            existing.Title = movie.Title;
            existing.Year = movie.Year;
            existing.Description = movie.Description;
            existing.PosterPath = movie.PosterPath;
            existing.CategoryId = movie.CategoryId;
            existing.DirectorId = movie.DirectorId;

            if (movie.PdfPath != null)
                existing.PdfPath = movie.PdfPath;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteMovieAsync(int id)
        {
            var movie = await _context.Movies
                .Include(m => m.Reviews)
                .Include(m => m.Casts)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie != null)
            {
                if (!string.IsNullOrEmpty(movie.PdfPath))
                {
                    var fullPath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        movie.PdfPath.TrimStart('/')
                    );

                    if (File.Exists(fullPath))
                        File.Delete(fullPath);
                }

                if (movie.Reviews != null && movie.Reviews.Any())
                    _context.Reviews.RemoveRange(movie.Reviews);

                if (movie.Casts != null && movie.Casts.Any())
                    _context.Casts.RemoveRange(movie.Casts);

                _context.Movies.Remove(movie);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<Director>> GetAllDirectorsAsync()
        {
            return await _context.Directors.ToListAsync();
        }

        public async Task<IEnumerable<Actor>> GetAllActorsAsync()
        {
            return await _context.Actors.ToListAsync();
        }

        public async Task DeleteCastsForMovieAsync(int movieId)
        {
            var casts = _context.Casts.Where(c => c.MovieId == movieId);
            _context.Casts.RemoveRange(casts);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<(Movie Movie, double Score)>> SearchMoviesAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Enumerable.Empty<(Movie, double)>();

            var movies = await _context.Movies
                .Include(m => m.Category)
                .Include(m => m.Director)
                .Include(m => m.Casts)!.ThenInclude(c => c.Actor)
                .Include(m => m.Reviews)
                .ToListAsync();

            var documents = movies.Select(m => new
            {
                Movie = m,
                Content = BuildSearchContent(m),
            }).ToList();

            var queryTerms = Tokenize(query);
            int totalDocs = documents.Count;

            var tokenizedDocs = documents.Select(d => new
            {
                d.Movie,
                d.Content,
                Terms = Tokenize(d.Content)
            }).ToList();

            var results = new List<(Movie Movie, double Score)>();

            foreach (var doc in tokenizedDocs)
            {
                double score = 0;

                foreach (var queryTerm in queryTerms)
                {
                    double tf = ComputeTF(queryTerm, doc.Terms);
                    double idf = ComputeIDF(queryTerm, tokenizedDocs.Select(d => d.Terms).ToList(), totalDocs);
                    score += tf * idf;

                    double prefixBonus = doc.Terms
                        .Where(t => t.StartsWith(queryTerm) && t != queryTerm)
                        .Sum(t => 0.6 / doc.Terms.Count);
                    score += prefixBonus;

                    double substringBonus = doc.Terms
                        .Where(t => t.Contains(queryTerm) && !t.StartsWith(queryTerm))
                        .Sum(t => 0.3 / doc.Terms.Count);
                    score += substringBonus;

                    if (doc.Movie.Title.ToLowerInvariant().Contains(queryTerm))
                        score += 2.0;
                }

                if (score > 0)
                    results.Add((doc.Movie, score));
            }

            return results.OrderByDescending(r => r.Score);
        }

        private string BuildSearchContent(Movie m)
        {
            var parts = new List<string> { m.Title };

            if (!string.IsNullOrEmpty(m.Description)) parts.Add(m.Description);
            if (m.Director != null) parts.Add(m.Director.Name);
            if (m.Category != null) parts.Add(m.Category.Name);

            if (m.Casts != null)
            {
                parts.AddRange(m.Casts
                    .Where(c => c.Actor != null)
                    .Select(c => c.Actor!.Name));
            }

            return string.Join(" ", parts).ToLowerInvariant();
        }

        private List<string> Tokenize(string text)
        {
            return text.ToLowerInvariant()
                .Split(new[] { ' ', ',', '.', '!', '?', '-', '\n', '\r', '(', ')', ':', ';' },
                    StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        private double ComputeTF(string term, List<string> docTerms)
        {
            if (docTerms.Count == 0) return 0;
            int count = docTerms.Count(t => t == term);
            return (double)count / docTerms.Count;
        }

        private double ComputeIDF(string term, List<List<string>> allDocs, int totalDocs)
        {
            int docsWithTerm = allDocs.Count(doc => doc.Contains(term));
            if (docsWithTerm == 0) return 0;
            return Math.Log((double)(totalDocs + 1) / (docsWithTerm + 1)) + 1;
        }

        public async Task AddCastsAsync(List<Cast> casts)
        {
            if (casts.Any())
            {
                _context.Casts.AddRange(casts);
                await _context.SaveChangesAsync();
            }
        }

        public async Task SetPdfPathAsync(int movieId, string pdfPath)
        {
            var movie = await _context.Movies.FindAsync(movieId);
            if (movie != null)
            {
                movie.PdfPath = pdfPath;
                await _context.SaveChangesAsync();
            }
        }

        // =========================
        // REVIEWS
        // =========================

        public async Task AddReviewAsync(Review review)
        {
            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> UserHasReviewedAsync(int movieId, string userId)
        {
            return await _context.Reviews
                .AnyAsync(r => r.MovieId == movieId && r.UserId == userId);
        }

        public async Task<double?> GetAverageRatingAsync(int movieId)
        {
            var ratings = await _context.Reviews
                .Where(r => r.MovieId == movieId)
                .Select(r => r.Rating)
                .ToListAsync();

            if (!ratings.Any())
                return null;

            return ratings.Average();
        }

        public async Task<Review?> GetUserReviewForMovieAsync(int movieId, string userId)
        {
            return await _context.Reviews
                .FirstOrDefaultAsync(r => r.MovieId == movieId && r.UserId == userId);
        }
    }
}