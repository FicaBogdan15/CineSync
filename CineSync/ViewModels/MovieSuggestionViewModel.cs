namespace CineSync.ViewModels
{
    public class MovieSuggestionViewModel
    {
        public int MovieId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public int Year { get; set; }
        public double Score { get; set; }
    }
}
