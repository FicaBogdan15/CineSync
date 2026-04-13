using CineSync.Data;
using CineSync.Models;
using CineSync.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CineSync.Services
{
    public class MovieService : IMovieService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;

        public MovieService(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public Task<IEnumerable<Movie>> GetMoviesAsync(int? categoryId)
        {
            return GetMoviesAsync(categoryId, 1, 0);
        }

        public async Task<IEnumerable<Movie>> GetMoviesAsync(int? categoryId, int page, int pageSize)
        {
            var query = _context.Movies
                .Include(m => m.Category)
                .Include(m => m.Reviews)
                .AsNoTracking()
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(m => m.CategoryId == categoryId.Value);

            query = query.OrderByDescending(m => m.MovieId);

            if (pageSize > 0)
            {
                var safePage = Math.Max(page, 1);
                query = query.Skip((safePage - 1) * pageSize).Take(pageSize);
            }

            return await query.ToListAsync();
        }

        public async Task<int> GetMoviesCountAsync(int? categoryId)
        {
            var query = _context.Movies.AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(m => m.CategoryId == categoryId.Value);

            return await query.CountAsync();
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

            _cache.Remove("all_movies_search");
            _cache.Remove($"similar_{movie.MovieId}_4");
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

            _cache.Remove("all_movies_search");
            _cache.Remove($"similar_{existing.MovieId}_4");
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

                _cache.Remove("all_movies_search");
                _cache.Remove($"similar_{movie.MovieId}_4");
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

            var movies = await GetAllMoviesForSearchAsync();

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

        public async Task<IEnumerable<MovieSuggestionViewModel>> GetMovieSuggestionsAsync(string query, int limit = 6)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Enumerable.Empty<MovieSuggestionViewModel>();

            query = query.Trim().ToLowerInvariant();

            var movies = await _context.Movies
                .Include(m => m.Category)
                .Select(m => new
                {
                    m.MovieId,
                    m.Title,
                    m.Year,
                    CategoryName = m.Category != null ? m.Category.Name : null
                })
                .ToListAsync();

            var suggestions = movies
                .Select(m => new MovieSuggestionViewModel
                {
                    MovieId = m.MovieId,
                    Title = m.Title,
                    Year = m.Year,
                    CategoryName = m.CategoryName,
                    Score = ComputeSuggestionScore(query, m.Title)
                })
                .Where(s => s.Score > 0)
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.Title)
                .Take(limit)
                .ToList();

            return suggestions;
        }

        public async Task<IEnumerable<SimilarMovieViewModel>> GetSimilarMoviesAsync(int movieId, int limit = 4)
        {
            var cacheKey = $"similar_{movieId}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<SimilarMovieViewModel>? cached))
                return cached!;

            var movies = await _context.Movies
                .Include(m => m.Category)
                .Include(m => m.Director)
                .Include(m => m.Casts)!.ThenInclude(c => c.Actor)
                .AsNoTracking()
                .ToListAsync();

            var targetMovie = movies.FirstOrDefault(m => m.MovieId == movieId);
            if (targetMovie == null)
                return Enumerable.Empty<SimilarMovieViewModel>();

            var targetActorIds = targetMovie.Casts?
                .Select(c => c.ActorId).ToHashSet() ?? new HashSet<int>();

            var targetCategoryId = targetMovie.CategoryId;
            var targetDirectorId = targetMovie.DirectorId;

            var targetTerms = Tokenize(BuildSearchContent(targetMovie));

            var candidates = movies
                .Where(m => m.MovieId != movieId &&
                    (m.CategoryId == targetCategoryId ||
                     (m.Casts?.Any(c => targetActorIds.Contains(c.ActorId)) == true) ||
                     m.DirectorId == targetDirectorId))
                .ToList();

            if (candidates.Count < limit * 2)
            {
                var extra = movies
                    .Where(m => m.MovieId != movieId && !candidates.Any(c => c.MovieId == m.MovieId))
                    .Take(50)
                    .ToList();
                candidates.AddRange(extra);
            }

            var pool = candidates.Take(100).ToList();
            pool.Add(targetMovie);

            var documents = pool.Select(m => new
            {
                Movie = m,
                Terms = Tokenize(BuildSearchContent(m))
            }).ToList();

            var allTerms = documents.SelectMany(d => d.Terms).Distinct().ToList();
            var allDocTerms = documents.Select(d => d.Terms).ToList();
            int totalDocs = documents.Count;

            var targetDoc = documents.First(d => d.Movie.MovieId == movieId);
            var targetVector = BuildTfidfVector(targetDoc.Terms, allDocTerms, allTerms, totalDocs);

            var results = documents
                .Where(d => d.Movie.MovieId != movieId)
                .Select(d => new SimilarMovieViewModel
                {
                    MovieId = d.Movie.MovieId,
                    Title = d.Movie.Title,
                    PosterPath = d.Movie.PosterPath,
                    CategoryName = d.Movie.Category?.Name,
                    Year = d.Movie.Year,
                    SimilarityScore = ComputeCosineSimilarity(
                        targetVector,
                        BuildTfidfVector(d.Terms, allDocTerms, allTerms, totalDocs))
                })
                .Where(x => x.SimilarityScore > 0)
                .OrderByDescending(x => x.SimilarityScore)
                .Take(limit)
                .ToList();

            _cache.Set(cacheKey, results, TimeSpan.FromMinutes(10));

            return results;
        }

        private async Task<List<Movie>> GetAllMoviesForSearchAsync()
        {
            const string cacheKey = "all_movies_search";

            if (_cache.TryGetValue(cacheKey, out List<Movie>? cached))
                return cached!;

            var movies = await _context.Movies
                .Include(m => m.Category)
                .Include(m => m.Director)
                .Include(m => m.Casts)!.ThenInclude(c => c.Actor)
                .Include(m => m.Reviews)
                .AsNoTracking()
                .ToListAsync();

            _cache.Set(cacheKey, movies, TimeSpan.FromMinutes(5));
            return movies;
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

        private double ComputeSuggestionScore(string query, string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return 0;

            var lowerTitle = title.ToLowerInvariant();
            double score = 0;

            if (lowerTitle == query)
                return 200;

            if (lowerTitle.StartsWith(query))
                score += 80;

            if (lowerTitle.Contains(query))
                score += 40;

            var words = lowerTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                if (word.StartsWith(query))
                    score += 20;
                else if (word.Contains(query))
                    score += 10;
            }

            int distFull = LevenshteinDistance(query, lowerTitle);
            int maxDist = Math.Max(1, (int)Math.Ceiling(query.Length * 0.4));
            if (distFull <= maxDist)
                score += Math.Max(0, (maxDist - distFull + 1) * 5.0);

            foreach (var word in words)
            {
                if (word.Length < query.Length - 2)
                    continue;

                int distWord = LevenshteinDistance(query, word);
                int maxDistWord = Math.Max(1, (int)Math.Ceiling(query.Length * 0.35));
                if (distWord <= maxDistWord)
                    score += Math.Max(0, (maxDistWord - distWord + 1) * 8.0);
            }

            return score;
        }

        private int LevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a))
                return b?.Length ?? 0;

            if (string.IsNullOrEmpty(b))
                return a.Length;

            int m = a.Length;
            int n = b.Length;

            var prev = new int[n + 1];
            var curr = new int[n + 1];

            for (int j = 0; j <= n; j++)
                prev[j] = j;

            for (int i = 1; i <= m; i++)
            {
                curr[0] = i;

                for (int j = 1; j <= n; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(
                        Math.Min(curr[j - 1] + 1, prev[j] + 1),
                        prev[j - 1] + cost
                    );
                }

                Array.Copy(curr, prev, n + 1);
            }

            return prev[n];
        }

        private Dictionary<string, double> BuildTfidfVector(
            List<string> docTerms,
            List<List<string>> allDocs,
            List<string> vocabulary,
            int totalDocs)
        {
            var vector = new Dictionary<string, double>();

            foreach (var term in vocabulary)
            {
                double tf = ComputeTF(term, docTerms);
                double idf = ComputeIDF(term, allDocs, totalDocs);
                vector[term] = tf * idf;
            }

            return vector;
        }

        private double ComputeCosineSimilarity(
            Dictionary<string, double> vectorA,
            Dictionary<string, double> vectorB)
        {
            double dotProduct = 0;
            double normA = 0;
            double normB = 0;

            foreach (var key in vectorA.Keys)
            {
                double a = vectorA[key];
                double b = vectorB.ContainsKey(key) ? vectorB[key] : 0;

                dotProduct += a * b;
                normA += a * a;
            }

            foreach (var value in vectorB.Values)
            {
                normB += value * value;
            }

            if (normA == 0 || normB == 0)
                return 0;

            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
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
