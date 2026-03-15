using CineSync.Models;
using System.ComponentModel.DataAnnotations;
using System.IO;

namespace CineSync.Models
{
    public class Movie
    {
        [Key]
        public int MovieId { get; set; }

        [Required]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        public int Year { get; set; }

        public string? Description { get; set; }
        public string? PosterPath { get; set; }
        public string? PdfPath { get; set; }

        public int? DirectorId { get; set; }
        public Director? Director { get; set; }

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public ICollection<Cast>? Casts { get; set; }
        public ICollection<Review>? Reviews { get; set; }
        public ICollection<AvailableOn>? AvailableOnPlatforms { get; set; }
    }
}