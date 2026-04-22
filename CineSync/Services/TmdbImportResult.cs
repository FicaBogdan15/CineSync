namespace CineSync.Services
{
    public sealed class TmdbImportResult
    {
        public int RequestedPages { get; set; }
        public int DiscoveredMovieCount { get; set; }
        public int ProcessedCount { get; set; }
        public int CreatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
    }
}
