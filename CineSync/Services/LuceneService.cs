using CineSync.Data;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.EntityFrameworkCore;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Directory = Lucene.Net.Store.Directory;

namespace CineSync.Services;

public class LuceneSearchResult
{
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string PdfPath { get; set; } = string.Empty;
    public float Score { get; set; }
    public string Fragment { get; set; } = string.Empty;
}

public class LuceneService
{
    private const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;

    private readonly IWebHostEnvironment _env;
    private readonly ApplicationDbContext _context;
    private readonly string _indexPath;

    public LuceneService(IWebHostEnvironment env, ApplicationDbContext context)
    {
        _env = env;
        _context = context;
        _indexPath = Path.Combine(_env.ContentRootPath, "App_Data", "lucene-index");
    }

    public async Task<int> BuildIndexAsync()
    {
        var movies = await _context.Movies
            .Where(m => !string.IsNullOrWhiteSpace(m.PdfPath))
            .AsNoTracking()
            .ToListAsync();

        System.IO.Directory.CreateDirectory(_indexPath);

        using var indexDir = FSDirectory.Open(new DirectoryInfo(_indexPath));
        using var analyzer = new StandardAnalyzer(AppLuceneVersion);

        var config = new IndexWriterConfig(AppLuceneVersion, analyzer)
        {
            OpenMode = OpenMode.CREATE
        };

        var indexedCount = 0;

        using var writer = new IndexWriter(indexDir, config);

        foreach (var movie in movies)
        {
            try
            {
                var pdfText = ExtractTextFromPdf(movie.PdfPath!);
                if (string.IsNullOrWhiteSpace(pdfText))
                {
                    continue;
                }

                var document = new Document
                {
                    new StringField("movieId", movie.MovieId.ToString(), Field.Store.YES),
                    new StringField("title", movie.Title ?? string.Empty, Field.Store.YES),
                    new StringField("pdfPath", movie.PdfPath ?? string.Empty, Field.Store.YES),
                    new TextField("titleText", movie.Title ?? string.Empty, Field.Store.YES),
                    new TextField("content", pdfText, Field.Store.YES)
                };

                writer.AddDocument(document);
                indexedCount++;
            }
            catch
            {
                // Ignore individual PDF failures so the rest of the index can still be built.
            }
        }

        writer.Commit();
        return indexedCount;
    }

    public List<LuceneSearchResult> Search(string queryText, string sortOrder = "desc", int maxResults = 20)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return [];
        }

        if (!System.IO.Directory.Exists(_indexPath))
        {
            return [];
        }

        using var indexDir = OpenIndexDirectory();
        if (!DirectoryReader.IndexExists(indexDir))
        {
            return [];
        }

        using var analyzer = new StandardAnalyzer(AppLuceneVersion);
        using var reader = DirectoryReader.Open(indexDir);
        var searcher = new IndexSearcher(reader);

        var parser = new MultiFieldQueryParser(
            AppLuceneVersion,
            ["content", "titleText"],
            analyzer);

        Query query;
        try
        {
            query = parser.Parse(QueryParserBase.Escape(queryText));
        }
        catch
        {
            query = new PrefixQuery(new Term("content", queryText.ToLowerInvariant()));
        }

        var hits = searcher.Search(query, maxResults);

        var results = hits.ScoreDocs
            .Select(hit =>
            {
                var doc = searcher.Doc(hit.Doc);
                var content = doc.Get("content") ?? string.Empty;

                return new LuceneSearchResult
                {
                    MovieId = int.TryParse(doc.Get("movieId"), out var movieId) ? movieId : 0,
                    Title = doc.Get("title") ?? string.Empty,
                    PdfPath = doc.Get("pdfPath") ?? string.Empty,
                    Score = hit.Score,
                    Fragment = ExtractFragment(content, queryText)
                };
            })
            .Where(result => result.MovieId > 0)
            .ToList();

        return string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase)
            ? results.OrderBy(result => result.Score).ToList()
            : results.OrderByDescending(result => result.Score).ToList();
    }

    public bool IndexExists()
    {
        if (!System.IO.Directory.Exists(_indexPath))
        {
            return false;
        }

        using var indexDir = OpenIndexDirectory();
        return DirectoryReader.IndexExists(indexDir);
    }

    private Directory OpenIndexDirectory()
    {
        System.IO.Directory.CreateDirectory(_indexPath);
        return FSDirectory.Open(new DirectoryInfo(_indexPath));
    }

    private string ExtractTextFromPdf(string pdfPath)
    {
        var fullPath = ResolvePdfPath(pdfPath);
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
        {
            return string.Empty;
        }

        var text = new System.Text.StringBuilder();

        using var reader = new PdfReader(fullPath);
        using var pdf = new PdfDocument(reader);

        for (var pageNumber = 1; pageNumber <= pdf.GetNumberOfPages(); pageNumber++)
        {
            var page = pdf.GetPage(pageNumber);
            text.AppendLine(PdfTextExtractor.GetTextFromPage(page));
        }

        return text.ToString();
    }

    private string ResolvePdfPath(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
        {
            return string.Empty;
        }

        if (pdfPath.StartsWith('/') || pdfPath.StartsWith('\\'))
        {
            var relativePath = pdfPath
                .TrimStart('/', '\\')
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            return Path.Combine(_env.WebRootPath, relativePath);
        }

        return Path.IsPathFullyQualified(pdfPath)
            ? pdfPath
            : Path.Combine(_env.ContentRootPath, pdfPath);
    }

    private static string ExtractFragment(string content, string query, int fragmentLength = 220)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var firstQueryTerm = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstQueryTerm))
        {
            return content.Length > fragmentLength
                ? $"{content[..fragmentLength].Trim()}..."
                : content.Trim();
        }

        var matchIndex = content.IndexOf(firstQueryTerm, StringComparison.OrdinalIgnoreCase);
        if (matchIndex < 0)
        {
            return content.Length > fragmentLength
                ? $"{content[..fragmentLength].Trim()}..."
                : content.Trim();
        }

        var start = Math.Max(0, matchIndex - 80);
        var length = Math.Min(fragmentLength, content.Length - start);
        var fragment = content.Substring(start, length).Trim();

        return $"{(start > 0 ? "..." : string.Empty)}{fragment}{(start + length < content.Length ? "..." : string.Empty)}";
    }
}
