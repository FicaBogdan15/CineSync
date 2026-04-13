namespace CineSync.Models
{
    public class PairingSession
    {
        public string Token { get; set; } = string.Empty;
        public string DesktopConnectionId { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public bool IsPaired { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(5);
    }
}
