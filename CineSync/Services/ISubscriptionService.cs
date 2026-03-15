using CineSync.Models;

namespace CineSync.Services
{
    public interface ISubscriptionService
    {
        // Public
        Task<IEnumerable<Subscription>> GetSubscriptionsAsync(int? platformId, string? sortBy);
        Task<IEnumerable<Platform>> GetAllPlatformsAsync();
        Task<Subscription?> GetByIdAsync(int id);

        // Admin CRUD
        Task AddAsync(Subscription subscription);
        Task UpdateAsync(Subscription subscription);
        Task DeleteAsync(int id);

        // Cart
        Task<Cart?> GetCartAsync(string userId);
        Task AddToCartAsync(string userId, int subscriptionId);
        Task RemoveFromCartAsync(string userId, int cartItemId);
        Task CheckoutAsync(string userId);

        Task<IEnumerable<(Subscription Subscription, double Score)>> SearchSubscriptionsAsync(string query);

    }
}