namespace CineSync.Integrations.Tmdb
{
    public class TmdbOptions
    {
        public string BearerToken { get; set; } = string.Empty;
        public string Language { get; set; } = "en-US";
        public string WatchRegion { get; set; } = "RO";
        public int MaxCastCount { get; set; } = 8;
        public string PosterSize { get; set; } = "w500";
    }
}
