using CineSync.Models;

namespace CineSync.Services
{
    public interface IMovieService
    {

        Task<IEnumerable<Movie>> GetMoviesAsync(int? catergoryID);
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


    }
}
