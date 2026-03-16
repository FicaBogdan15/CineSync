using System.ComponentModel.DataAnnotations;

namespace CineSync.Models
{
    public class WatchList
    {
        [Key]
        public int WatchListId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public ICollection<WatchListItem> Items { get; set; } = new List<WatchListItem>();
    }
}