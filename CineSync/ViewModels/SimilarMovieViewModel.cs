namespace CineSync.ViewModels
{
    public class SimilarMovieViewModel
    {
        public int MovieId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public string? CategoryName { get; set; }
        public int Year { get; set; }
        public double SimilarityScore { get; set; }
    }
}
