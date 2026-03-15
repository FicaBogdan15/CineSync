using System.ComponentModel.DataAnnotations;

namespace CineSync.Models
{
    public class Director
    {

        [Key]
        public int DirectorId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public int AwardsCount { get; set; }
        public int DirectedMoviesCount { get; set; }

        public ICollection<Movie>? Movies { get; set; }
    }
}
