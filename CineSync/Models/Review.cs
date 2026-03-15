using CineSync.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CineSync.Models
{
    public class Review
    {
        [Key]
        public int ReviewId { get; set; }

        [Required]
        public string ReviewText { get; set; } = string.Empty;

        [Range(1, 10)]
        public int Rating { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        public int MovieId { get; set; }
        public Movie? Movie { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
    }
}