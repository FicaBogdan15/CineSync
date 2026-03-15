using Microsoft.AspNetCore.Identity;

namespace CineSync.Models
{
    public class ApplicationUser : IdentityUser
    {

        public DateTime SubscriptionStartDate { get; set; }
        public DateTime SubscriptionExpiryDate { get; set; }

        public int? SubscriptionId { get; set; }
        public Subscription? Subscription { get; set; }

        public ICollection<Review>? Reviews { get; set; }

    }
}
