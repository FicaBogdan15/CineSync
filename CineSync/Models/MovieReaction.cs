using System.ComponentModel.DataAnnotations;

namespace CineSync.Models
{
    public class MovieReaction
    {
        [Key]
        public int MovieReactionId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public int MovieId { get; set; }
        public Movie? Movie { get; set; }

        // true = Like, false = Dislike
        public bool IsLiked { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;
    }
}