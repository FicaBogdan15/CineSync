using System.ComponentModel.DataAnnotations;

namespace CineSync.Models
{
    public class WatchListItem
    {
        [Key]
        public int WatchListItemId { get; set; }

        public int WatchListId { get; set; }
        public WatchList? WatchList { get; set; }

        public int MovieId { get; set; }
        public Movie? Movie { get; set; }

        public bool IsWatched { get; set; } = false;

        public DateTime AddedAt { get; set; } = DateTime.Now;
        public DateTime? WatchedAt { get; set; }
    }
}