using System.ComponentModel.DataAnnotations;

namespace CineSync.Models
{
    public class Cast
    {
        [Key]
        public int CastId { get; set; }

        public int MovieId { get; set; }
        public Movie? Movie { get; set; }

        public int ActorId { get; set; }
        public Actor? Actor { get; set; }

        [StringLength(20)]
        public string RoleType { get; set; } = string.Empty; 

        [StringLength(100)]
        public string CharacterName { get; set; } = string.Empty;


    }
}
