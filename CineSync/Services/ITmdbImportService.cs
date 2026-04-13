namespace CineSync.Services
{
    public interface ITmdbImportService
    {
        Task<TmdbImportResult> ImportNetflixCatalogAsync(int maxPages, bool generatePdfs, CancellationToken cancellationToken = default);
    }
}
