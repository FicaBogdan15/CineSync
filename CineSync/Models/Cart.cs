using System.ComponentModel.DataAnnotations;

namespace CineSync.Models
{
    public class Cart
    {
        [Key]
        public int CartId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    }
}