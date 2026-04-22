using CineSync.Data;
using CineSync.Models;
using CineSync.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CineSync.Controllers.Api
{
    [ApiController]
    [Route("api/profile")]
    [Authorize(AuthenticationSchemes = "Mobile")]
    public class ProfileApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly PairingService _pairingService;

        public ProfileApiController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            PairingService pairingService)
        {
            _userManager = userManager;
            _context = context;
            _pairingService = pairingService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Unauthorized();
            }

            var reviewCount = await _context.Reviews
                .AsNoTracking()
                .CountAsync(review => review.UserId == userId);

            var watchlistCount = await _context.WatchListItems
                .AsNoTracking()
                .CountAsync(item => item.WatchList != null && item.WatchList.UserId == userId);

            var sessions = _pairingService.GetActiveSessionsForUser(userId);

            return Ok(new ProfileResponse
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                ReviewCount = reviewCount,
                WatchlistCount = watchlistCount,
                SyncStatus = MobileApiMapper.BuildSyncStatus(sessions),
                ConnectedDevices = MobileApiMapper.BuildConnectedDevices(sessions)
            });
        }
    }
}
