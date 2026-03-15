using System.ComponentModel.DataAnnotations;

namespace CineSync.Models
{
    public class AvailableOn
    {
        [Key]
        public int AvailableOnId { get; set; }

        public int MovieId { get; set; }
        public Movie? Movie { get; set; }

        public int PlatformId { get; set; }
        public Platform? Platform { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
