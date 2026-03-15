using CineSync.Models;
using CineSync.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CineSync.Controllers
{
    public class SubscriptionController : Controller
    {
        private readonly ISubscriptionService _service;

        public SubscriptionController(ISubscriptionService service)
        {
            _service = service;
        }

        // Public
        public async Task<IActionResult> Index(int? platformId, string? sortBy, string? search)
        {
            ViewBag.Platforms = await _service.GetAllPlatformsAsync();
            ViewBag.SelectedPlatform = platformId;
            ViewBag.SortBy = sortBy;
            ViewBag.Search = search;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var results = await _service.SearchSubscriptionsAsync(search);
                if (platformId.HasValue)
                    results = results.Where(r => r.Subscription.PlatformId == platformId.Value);
                ViewBag.SearchResults = results.ToList();
                return View(results.Select(r => r.Subscription).ToList());
            }

            return View(await _service.GetSubscriptionsAsync(platformId, sortBy));
        }


        public async Task<IActionResult> Details(int id)
        {
            var sub = await _service.GetByIdAsync(id);
            if (sub == null) return NotFound();
            return View(sub);
        }

        // Cart 
        [Authorize]
        public async Task<IActionResult> Cart()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var cart = await _service.GetCartAsync(userId);
            return View(cart);
        }

        [HttpPost, Authorize]
        public async Task<IActionResult> AddToCart(int subscriptionId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _service.AddToCartAsync(userId, subscriptionId);
            return RedirectToAction("Index");
        }

        [HttpPost, Authorize]
        public async Task<IActionResult> RemoveFromCart(int cartItemId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _service.RemoveFromCartAsync(userId, cartItemId);
            return RedirectToAction("Cart");
        }

        [HttpPost, Authorize]
        public async Task<IActionResult> Checkout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _service.CheckoutAsync(userId);
            TempData["Success"] = "Order placed successfully!";
            return RedirectToAction("Cart");
        }

        // Admin CRUD 
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage()
            => View(await _service.GetSubscriptionsAsync(null, null));

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Platforms = await _service.GetAllPlatformsAsync();
            return View(new Subscription());
        }

        [HttpPost, Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Subscription subscription)
        {
            ModelState.Remove("Platform");
            if (!ModelState.IsValid)
            {
                ViewBag.Platforms = await _service.GetAllPlatformsAsync();
                return View(subscription);
            }
            await _service.AddAsync(subscription);
            return RedirectToAction("Manage");
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var sub = await _service.GetByIdAsync(id);
            if (sub == null) return NotFound();
            ViewBag.Platforms = await _service.GetAllPlatformsAsync();
            return View(sub);
        }

        [HttpPost, Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Subscription subscription)
        {
            ModelState.Remove("Platform");
            if (!ModelState.IsValid)
            {
                ViewBag.Platforms = await _service.GetAllPlatformsAsync();
                return View(subscription);
            }
            await _service.UpdateAsync(subscription);
            return RedirectToAction("Manage");
        }

        [HttpPost, Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return RedirectToAction("Manage");
        }
    }
}