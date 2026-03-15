using CineSync.Models;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

namespace CineSync.Services
{
    public class PdfService : IPdfService
    {
        private readonly IWebHostEnvironment _env;

        public PdfService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public string GenerateMoviePdf(Movie movie)
        {
            var folder = Path.Combine(_env.WebRootPath, "pdfs");
            Directory.CreateDirectory(folder);

            var fileName = $"movie_{movie.MovieId}.pdf";
            var filePath = Path.Combine(folder, fileName);

            for (int i = 0; i < 5; i++)
            {
                try
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                    break;
                }
                catch (IOException)
                {
                    Thread.Sleep(300);
                }
            }

            using (var writer = new PdfWriter(filePath))
            using (var pdf = new PdfDocument(writer))
            using (var doc = new Document(pdf))
            {
                doc.Add(new Paragraph(movie.Title)
                    .SetFontSize(24).SetBold());
                doc.Add(new Paragraph($"Year: {movie.Year}"));
                doc.Add(new Paragraph($"Category: {movie.Category?.Name ?? "N/A"}"));
                doc.Add(new Paragraph($"Director: {movie.Director?.Name ?? "N/A"}"));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph("Description:").SetBold());
                doc.Add(new Paragraph(movie.Description ?? "No description."));

                if (movie.Casts != null && movie.Casts.Any())
                {
                    doc.Add(new Paragraph(" "));
                    doc.Add(new Paragraph("Cast:").SetBold());
                    foreach (var cast in movie.Casts)
                    {
                        doc.Add(new Paragraph(
                            $"- {cast.Actor?.Name ?? "?"} as {cast.CharacterName} ({cast.RoleType})"
                        ));
                    }
                }
            }

            return $"/pdfs/{fileName}";
        }
    }
}