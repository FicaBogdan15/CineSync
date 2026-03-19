using CineSync.Models;
using CineSync.ViewModels;

namespace CineSync.Services
{
    public interface IMovieService
    {

        Task<IEnumerable<Movie>> GetMoviesAsync(int? catergoryID);
        Task<IEnumerable<Movie>> GetMoviesAsync(int? catergoryID, int page, int pageSize);
        Task<int> GetMoviesCountAsync(int? categoryId);
        Task<Movie?> GetMovieByIdAsync(int id);
        Task<IEnumerable<Category>> GetAllCategoriesAsync();
        Task<IEnumerable<(Movie Movie, double Score)>> SearchMoviesAsync(string query);
        Task AddMovieAsync(Movie movie);
        Task UpdateMovieAsync(Movie movie);
        Task DeleteMovieAsync(int id);
        Task<IEnumerable<Director>> GetAllDirectorsAsync();
        Task<IEnumerable<Actor>> GetAllActorsAsync();
        Task DeleteCastsForMovieAsync(int movieId);

        Task AddCastsAsync(List<Cast> casts);
        Task SetPdfPathAsync(int movieId, string pdfPath);



        // REVIEW METHODS
        Task AddReviewAsync(Review review);
        Task<bool> UserHasReviewedAsync(int movieId, string userId);
        Task<double?> GetAverageRatingAsync(int movieId);
        Task<Review?> GetUserReviewForMovieAsync(int movieId, string userId);
        Task<IEnumerable<MovieSuggestionViewModel>> GetMovieSuggestionsAsync(string query, int limit = 6);
        Task<IEnumerable<SimilarMovieViewModel>> GetSimilarMoviesAsync(int movieId, int limit = 4);


    }
}
