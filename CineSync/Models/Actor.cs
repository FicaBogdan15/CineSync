using System.ComponentModel.DataAnnotations;

namespace CineSync.Models
{
    public class Actor
    {
        [Key]
        public int ActorId { get; set; }

        public int? TmdbPersonId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public DateTime DebutDate { get; set; }
        public int AwardsCount { get; set; }

        public ICollection<Cast>? Casts { get; set; }

    }
}
