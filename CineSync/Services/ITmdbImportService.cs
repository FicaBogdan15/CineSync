namespace CineSync.Services
{
    public interface ITmdbImportService
    {
        Task<global::CineSync.Services.TmdbImportResult> ImportNetflixCatalogAsync(
            int maxPages,
            bool generatePdfs,
            CancellationToken cancellationToken = default);
    }
}
