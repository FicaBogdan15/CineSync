using CineSync.Data;
using CineSync.Integrations.Tmdb;
using CineSync.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CineSync.Services
{
    public class TmdbImportService : ITmdbImportService
    {
        private static readonly string[] CategoryPriority =
        [
            "Documentary",
            "Animation",
            "Horror",
            "Sci-Fi",
            "Fantasy",
            "Thriller",
            "Romance",
            "Comedy",
            "Action",
            "Drama"
        ];

        private static readonly Dictionary<string, string> GenreMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Action"] = "Action",
            ["Comedy"] = "Comedy",
            ["Drama"] = "Drama",
            ["Horror"] = "Horror",
            ["Science Fiction"] = "Sci-Fi",
            ["Romance"] = "Romance",
            ["Documentary"] = "Documentary",
            ["Thriller"] = "Thriller",
            ["Fantasy"] = "Fantasy",
            ["Animation"] = "Animation"
        };

        private readonly ApplicationDbContext _context;
        private readonly ITmdbClient _tmdbClient;
        private readonly IPdfService _pdfService;
        private readonly TmdbOptions _options;
        private readonly ILogger<TmdbImportService> _logger;

        public TmdbImportService(
            ApplicationDbContext context,
            ITmdbClient tmdbClient,
            IPdfService pdfService,
            IOptions<TmdbOptions> options,
            ILogger<TmdbImportService> logger)
        {
            _context = context;
            _tmdbClient = tmdbClient;
            _pdfService = pdfService;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<TmdbImportResult> ImportNetflixCatalogAsync(int maxPages, bool generatePdfs, CancellationToken cancellationToken = default)
        {
            maxPages = Math.Clamp(maxPages, 1, 25);

            var providerId = await _tmdbClient.GetNetflixProviderIdAsync(cancellationToken);
            if (!providerId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Could not resolve the Netflix watch provider from TMDb for region '{_options.WatchRegion}'.");
            }

            var categories = await LoadCategoryLookupAsync(cancellationToken);
            var netflixPlatform = await GetOrCreateNetflixPlatformAsync(cancellationToken);
            var movieIds = await _tmdbClient.DiscoverMovieIdsByProviderAsync(providerId.Value, maxPages, cancellationToken);

            var result = new TmdbImportResult
            {
                RequestedPages = maxPages,
                DiscoveredMovieCount = movieIds.Count
            };

            foreach (var tmdbMovieId in movieIds)
            {
                result.ProcessedCount++;

                try
                {
                    var importStatus = await ImportMovieAsync(
                        tmdbMovieId,
                        categories,
                        netflixPlatform,
                        generatePdfs,
                        cancellationToken);

                    switch (importStatus)
                    {
                        case ImportMovieStatus.Created:
                            result.CreatedCount++;
                            break;
                        case ImportMovieStatus.Updated:
                            result.UpdatedCount++;
                            break;
                        case ImportMovieStatus.Skipped:
                            result.SkippedCount++;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    _logger.LogError(ex, "Failed to import TMDb movie {TmdbMovieId}", tmdbMovieId);
                }
            }

            netflixPlatform.AvailableMoviesCount = await _context.AvailableOnPlatforms
                .CountAsync(entry => entry.PlatformId == netflixPlatform.PlatformId, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            return result;
        }

        private async Task<Dictionary<string, Category>> LoadCategoryLookupAsync(CancellationToken cancellationToken)
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var lookup = categories.ToDictionary(category => category.Name, StringComparer.OrdinalIgnoreCase);
            var missingCategories = GenreMap.Values
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(name => !lookup.ContainsKey(name))
                .ToList();

            if (missingCategories.Any())
            {
                throw new InvalidOperationException(
                    "The following categories are missing from the database: "
                    + string.Join(", ", missingCategories));
            }

            return lookup;
        }

        private async Task<ImportMovieStatus> ImportMovieAsync(
            int tmdbMovieId,
            Dictionary<string, Category> categories,
            Platform netflixPlatform,
            bool generatePdfs,
            CancellationToken cancellationToken)
        {
            var details = await _tmdbClient.GetMovieDetailsAsync(tmdbMovieId, cancellationToken);
            if (details == null
                || string.IsNullOrWhiteSpace(details.Title)
                || string.IsNullOrWhiteSpace(details.ReleaseDate)
                || string.IsNullOrWhiteSpace(details.PosterPath))
            {
                return ImportMovieStatus.Skipped;
            }

            if (!DateTime.TryParse(details.ReleaseDate, out var releaseDate))
                return ImportMovieStatus.Skipped;

            var category = ResolveCategory(details.Genres, categories);
            if (category == null)
                return ImportMovieStatus.Skipped;

            var credits = await _tmdbClient.GetMovieCreditsAsync(tmdbMovieId, cancellationToken) ?? new TmdbMovieCredits();
            var director = await UpsertDirectorAsync(credits, cancellationToken);

            var movie = await _context.Movies
                .FirstOrDefaultAsync(m => m.TmdbId == tmdbMovieId, cancellationToken);

            var isCreated = false;

            if (movie == null)
            {
                movie = await _context.Movies
                    .FirstOrDefaultAsync(
                        m => m.TmdbId == null
                             && m.Title == details.Title
                             && m.Year == releaseDate.Year,
                        cancellationToken);

                if (movie == null)
                {
                    movie = new Movie();
                    _context.Movies.Add(movie);
                    isCreated = true;
                }
            }

            movie.TmdbId = tmdbMovieId;
            movie.Title = TrimOrDefault(details.Title, 150, "Untitled");
            movie.Year = releaseDate.Year;
            movie.Description = string.IsNullOrWhiteSpace(details.Overview)
                ? "Description not available."
                : details.Overview.Trim();
            movie.PosterPath = BuildPosterUrl(details.PosterPath);
            movie.CategoryId = category.CategoryId;
            movie.DirectorId = director?.DirectorId;

            await _context.SaveChangesAsync(cancellationToken);

            await ReplaceCastAsync(movie.MovieId, credits.Cast, cancellationToken);
            await EnsureNetflixAvailabilityAsync(movie.MovieId, netflixPlatform, cancellationToken);

            if (generatePdfs)
            {
                var movieForPdf = await _context.Movies
                    .Include(m => m.Category)
                    .Include(m => m.Director)
                    .Include(m => m.Casts)
                        .ThenInclude(c => c.Actor)
                    .FirstAsync(m => m.MovieId == movie.MovieId, cancellationToken);

                movie.PdfPath = _pdfService.GenerateMoviePdf(movieForPdf);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return isCreated ? ImportMovieStatus.Created : ImportMovieStatus.Updated;
        }

        private Category? ResolveCategory(IEnumerable<TmdbMovieGenre> genres, Dictionary<string, Category> categories)
        {
            var mappedNames = genres
                .Select(genre => GenreMap.TryGetValue(genre.Name, out var mappedName) ? mappedName : null)
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var categoryName in CategoryPriority)
            {
                if (mappedNames.Contains(categoryName) && categories.TryGetValue(categoryName, out var category))
                    return category;
            }

            return null;
        }

        private async Task<Director?> UpsertDirectorAsync(TmdbMovieCredits credits, CancellationToken cancellationToken)
        {
            var directorCredit = credits.Crew
                .FirstOrDefault(crew =>
                    string.Equals(crew.Job, "Director", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(crew.Name));

            if (directorCredit == null)
                return null;

            Director? director = null;

            if (directorCredit.PersonId > 0)
            {
                director = await _context.Directors
                    .FirstOrDefaultAsync(d => d.TmdbPersonId == directorCredit.PersonId, cancellationToken);
            }

            if (director == null)
            {
                director = await _context.Directors
                    .FirstOrDefaultAsync(d => d.Name == directorCredit.Name, cancellationToken);
            }

            if (director == null)
            {
                director = new Director
                {
                    TmdbPersonId = directorCredit.PersonId > 0 ? directorCredit.PersonId : null,
                    Name = TrimOrDefault(directorCredit.Name, 100, "Unknown Director"),
                    AwardsCount = 0,
                    DirectedMoviesCount = 0
                };

                _context.Directors.Add(director);
                await _context.SaveChangesAsync(cancellationToken);
                return director;
            }

            director.Name = TrimOrDefault(directorCredit.Name, 100, director.Name);

            if (director.TmdbPersonId == null && directorCredit.PersonId > 0)
                director.TmdbPersonId = directorCredit.PersonId;

            await _context.SaveChangesAsync(cancellationToken);
            return director;
        }

        private async Task ReplaceCastAsync(int movieId, IEnumerable<TmdbCastMember> castMembers, CancellationToken cancellationToken)
        {
            var existingCasts = await _context.Casts
                .Where(cast => cast.MovieId == movieId)
                .ToListAsync(cancellationToken);

            if (existingCasts.Any())
            {
                _context.Casts.RemoveRange(existingCasts);
                await _context.SaveChangesAsync(cancellationToken);
            }

            var selectedCast = castMembers
                .Where(member => !string.IsNullOrWhiteSpace(member.Name))
                .OrderBy(member => member.Order)
                .Take(Math.Max(1, _options.MaxCastCount))
                .ToList();

            for (var index = 0; index < selectedCast.Count; index++)
            {
                var castMember = selectedCast[index];
                var actor = await UpsertActorAsync(castMember, cancellationToken);
                if (actor == null)
                    continue;

                _context.Casts.Add(new Cast
                {
                    MovieId = movieId,
                    ActorId = actor.ActorId,
                    CharacterName = TrimOrDefault(castMember.Character, 100, "Unknown"),
                    RoleType = index < 3 ? "Lead" : "Support"
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<Actor?> UpsertActorAsync(TmdbCastMember castMember, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(castMember.Name))
                return null;

            Actor? actor = null;

            if (castMember.PersonId > 0)
            {
                actor = await _context.Actors
                    .FirstOrDefaultAsync(a => a.TmdbPersonId == castMember.PersonId, cancellationToken);
            }

            if (actor == null)
            {
                actor = await _context.Actors
                    .FirstOrDefaultAsync(a => a.Name == castMember.Name, cancellationToken);
            }

            if (actor == null)
            {
                actor = new Actor
                {
                    TmdbPersonId = castMember.PersonId > 0 ? castMember.PersonId : null,
                    Name = TrimOrDefault(castMember.Name, 100, "Unknown Actor"),
                    DebutDate = DateTime.UtcNow,
                    AwardsCount = 0
                };

                _context.Actors.Add(actor);
                await _context.SaveChangesAsync(cancellationToken);
                return actor;
            }

            actor.Name = TrimOrDefault(castMember.Name, 100, actor.Name);

            if (actor.TmdbPersonId == null && castMember.PersonId > 0)
                actor.TmdbPersonId = castMember.PersonId;

            await _context.SaveChangesAsync(cancellationToken);
            return actor;
        }

        private async Task EnsureNetflixAvailabilityAsync(int movieId, Platform netflixPlatform, CancellationToken cancellationToken)
        {
            var existingAvailability = await _context.AvailableOnPlatforms
                .FirstOrDefaultAsync(
                    availability => availability.MovieId == movieId && availability.PlatformId == netflixPlatform.PlatformId,
                    cancellationToken);

            if (existingAvailability != null)
            {
                existingAvailability.Status = "Streaming";
                return;
            }

            _context.AvailableOnPlatforms.Add(new AvailableOn
            {
                MovieId = movieId,
                PlatformId = netflixPlatform.PlatformId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddYears(1),
                Status = "Streaming"
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<Platform> GetOrCreateNetflixPlatformAsync(CancellationToken cancellationToken)
        {
            var platform = await _context.Platforms
                .FirstOrDefaultAsync(p => p.Name == "Netflix", cancellationToken);

            if (platform != null)
                return platform;

            platform = new Platform
            {
                Name = "Netflix",
                URL = "https://www.netflix.com/ro-en/",
                AvailableMoviesCount = 0,
                Price = 0m
            };

            _context.Platforms.Add(platform);
            await _context.SaveChangesAsync(cancellationToken);

            return platform;
        }

        private string BuildPosterUrl(string posterPath)
        {
            var normalizedPath = posterPath.StartsWith('/')
                ? posterPath
                : "/" + posterPath;

            var posterSize = string.IsNullOrWhiteSpace(_options.PosterSize)
                ? "w500"
                : _options.PosterSize.Trim();

            return $"https://image.tmdb.org/t/p/{posterSize}{normalizedPath}";
        }

        private static string TrimOrDefault(string? value, int maxLength, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength
                ? trimmed
                : trimmed[..maxLength];
        }

        private enum ImportMovieStatus
        {
            Created,
            Updated,
            Skipped
        }
    }
}
