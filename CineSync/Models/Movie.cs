using System.ComponentModel.DataAnnotations;

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

        public ICollection<Cast> Casts { get; set; } = new List<Cast>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<AvailableOn> AvailableOnPlatforms { get; set; } = new List<AvailableOn>();

        public ICollection<MovieReaction> MovieReactions { get; set; } = new List<MovieReaction>();
        public ICollection<WatchListItem> WatchListItems { get; set; } = new List<WatchListItem>();
    }
}