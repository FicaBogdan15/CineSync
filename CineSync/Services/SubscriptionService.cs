using CineSync.Data;
using CineSync.Models;
using Microsoft.EntityFrameworkCore;

namespace CineSync.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ApplicationDbContext _context;

        public SubscriptionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Subscription>> GetSubscriptionsAsync(int? platformId, string? sortBy)
        {
            var query = _context.Subscriptions.Include(s => s.Platform).AsQueryable();

            if (platformId.HasValue)
                query = query.Where(s => s.PlatformId == platformId.Value);

            query = sortBy switch
            {
                "price_asc" => query.OrderBy(s => s.MonthlyPrice),
                "price_desc" => query.OrderByDescending(s => s.MonthlyPrice),
                _ => query
            };

            return await query.ToListAsync();
        }

        public async Task<IEnumerable<Platform>> GetAllPlatformsAsync()
            => await _context.Platforms.ToListAsync();

        public async Task<Subscription?> GetByIdAsync(int id)
            => await _context.Subscriptions.Include(s => s.Platform).FirstOrDefaultAsync(s => s.SubscriptionId == id);

        public async Task AddAsync(Subscription subscription)
        {
            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Subscription subscription)
        {
            _context.Subscriptions.Update(subscription);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var sub = await _context.Subscriptions.FindAsync(id);
            if (sub != null)
            {
                _context.Subscriptions.Remove(sub);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Cart?> GetCartAsync(string userId)
        {
            return await _context.Carts
                .Include(c => c.CartItems)!
                    .ThenInclude(ci => ci.Subscription)!
                        .ThenInclude(s => s!.Platform)
                .FirstOrDefaultAsync(c => c.UserId == userId);
        }

        public async Task AddToCartAsync(string userId, int subscriptionId)
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart { UserId = userId };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            if (cart.CartItems.Any(ci => ci.SubscriptionId == subscriptionId))
                return;

            cart.CartItems.Add(new CartItem
            {
                CartId = cart.CartId,
                SubscriptionId = subscriptionId
            });

            await _context.SaveChangesAsync();
        }

        public async Task RemoveFromCartAsync(string userId, int cartItemId)
        {
            var item = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId && ci.Cart!.UserId == userId);

            if (item != null)
            {
                _context.CartItems.Remove(item);
                await _context.SaveChangesAsync();
            }
        }

        public async Task CheckoutAsync(string userId)
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart != null)
            {
                _context.CartItems.RemoveRange(cart.CartItems);
                await _context.SaveChangesAsync();
            }
        }


        public async Task<IEnumerable<(Subscription Subscription, double Score)>> SearchSubscriptionsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Enumerable.Empty<(Subscription, double)>();

            var subscriptions = await _context.Subscriptions
                .Include(s => s.Platform)
                .ToListAsync();

            var documents = subscriptions.Select(s => new
            {
                Subscription = s,
                Terms = Tokenize(BuildSearchContent(s))
            }).ToList();

            var queryTerms = Tokenize(query);
            int totalDocs = documents.Count;
            var results = new List<(Subscription, double Score)>();

            foreach (var doc in documents)
            {
                double score = 0;

                foreach (var queryTerm in queryTerms)
                {
                    // TF-IDF 
                    double tf = ComputeTF(queryTerm, doc.Terms);
                    double idf = ComputeIDF(queryTerm, documents.Select(d => d.Terms).ToList(), totalDocs);
                    score += tf * idf;

                    // Prefix match
                    score += doc.Terms
                        .Where(t => t.StartsWith(queryTerm) && t != queryTerm)
                        .Sum(t => 0.6 / doc.Terms.Count);

                    // Substring match
                    score += doc.Terms
                        .Where(t => t.Contains(queryTerm) && !t.StartsWith(queryTerm))
                        .Sum(t => 0.3 / doc.Terms.Count);

                    //  titlu/platform direct
                    if (doc.Subscription.Platform?.Name.ToLowerInvariant().Contains(queryTerm) == true)
                        score += 2.0;
                    if (doc.Subscription.Type.ToLowerInvariant().Contains(queryTerm))
                        score += 1.5;
                }

                if (score > 0)
                    results.Add((doc.Subscription, score));
            }

            return results.OrderByDescending(r => r.Score);
        }

        private string BuildSearchContent(Subscription s)
        {
            var parts = new List<string> { s.Type, s.VideoQuality };
            if (s.Platform != null) parts.Add(s.Platform.Name);
            return string.Join(" ", parts).ToLowerInvariant();
        }

        private List<string> Tokenize(string text)
        {
            return text.ToLowerInvariant()
                .Split(new[] { ' ', ',', '.', '!', '?', '-', '\n', '\r', '(', ')', ':', ';' },
                       StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        private double ComputeTF(string term, List<string> docTerms)
        {
            if (docTerms.Count == 0) return 0;
            return (double)docTerms.Count(t => t == term) / docTerms.Count;
        }

        private double ComputeIDF(string term, List<List<string>> allDocs, int totalDocs)
        {
            int docsWithTerm = allDocs.Count(doc => doc.Contains(term));
            if (docsWithTerm == 0) return 0;
            return Math.Log((double)(totalDocs + 1) / (docsWithTerm + 1)) + 1;
        }
    }
}