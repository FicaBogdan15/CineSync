using System.ComponentModel.DataAnnotations;

namespace CineSync.Models
{
    public class Subscription
    {

        [Key]
        public int SubscriptionId { get; set; }

        [StringLength(50)]
        public string Type { get; set; } = string.Empty; // ex: Monthly, Annual

        public decimal MonthlyPrice { get; set; }
        public string VideoQuality { get; set; } = string.Empty;

        public int PlatformId { get; set; }
        public Platform? Platform { get; set; }
        public ICollection<ApplicationUser>? Users { get; set; }
    }
}
