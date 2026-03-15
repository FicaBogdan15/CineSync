using CineSync.Models;

namespace CineSync.Services
{
    public interface IPdfService
    {
        string GenerateMoviePdf(Movie movie);
    }
}