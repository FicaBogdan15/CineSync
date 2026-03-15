using System.ComponentModel.DataAnnotations;

namespace CineSync.Models
{
    public class CartItem
    {
        [Key]
        public int CartItemId { get; set; }
        public int CartId { get; set; }
        public Cart? Cart { get; set; }
        public int SubscriptionId { get; set; }
        public Subscription? Subscription { get; set; }
    }
}