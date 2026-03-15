using System.ComponentModel.DataAnnotations;

namespace CineSync.Models
{
    public class Platform
    {
        [Key]
        public int PlatformId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? URL { get; set; }
        public int AvailableMoviesCount { get; set; }
        public decimal Price { get; set; }

        public ICollection<AvailableOn>? AvailableMovies { get; set; }
        public ICollection<Subscription>? Subscriptions { get; set; }
    }
}
