using CineSync.Hubs;
using CineSync.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace CineSync.Controllers.Api
{
    [ApiController]
    [Route("api/sync")]
    [Authorize(AuthenticationSchemes = "Mobile")]
    public class SyncApiController : ControllerBase
    {
        private readonly PairingService _pairingService;
        private readonly IHubContext<RemoteHub> _hubContext;

        public SyncApiController(
            PairingService pairingService,
            IHubContext<RemoteHub> hubContext)
        {
            _pairingService = pairingService;
            _hubContext = hubContext;
        }

        [HttpGet("status")]
        public IActionResult Status()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var sessions = _pairingService.GetActiveSessionsForUser(userId);

            return Ok(new SyncStatusResponse
            {
                HasActiveSession = sessions.Count > 0,
                ActiveSessionCount = sessions.Count,
                PairedSessionCount = sessions.Count(session => session.IsPaired),
                SyncStatus = MobileApiMapper.BuildSyncStatus(sessions),
                ConnectedDevices = MobileApiMapper.BuildConnectedDevices(sessions)
            });
        }

        [HttpPost("pair")]
        public async Task<IActionResult> Pair([FromBody] SyncPairRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? "Authenticated User";
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var session = _pairingService.GetSession(request.Token);
            if (session == null)
            {
                return NotFound(new { error = "Pairing session not found or expired." });
            }

            if (!string.Equals(session.UserId, userId, StringComparison.Ordinal))
            {
                return Forbid();
            }

            if (!_pairingService.MarkPaired(request.Token, userId, request.DeviceName ?? "Desktop station"))
            {
                return BadRequest(new { error = "Unable to pair session." });
            }

            await _hubContext.Clients.Group($"desktop_{request.Token}")
                .SendAsync("PhonePaired", userName);

            return Ok(new { ok = true, syncStatus = "PAIRED" });
        }

        [HttpPost("action")]
        public async Task<IActionResult> Action([FromBody] SyncActionRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var session = _pairingService.GetSession(request.Token);
            if (session == null || !session.IsPaired)
            {
                return Unauthorized();
            }

            if (!string.Equals(session.UserId, userId, StringComparison.Ordinal))
            {
                return Forbid();
            }

            await _hubContext.Clients.Group($"desktop_{request.Token}")
                .SendAsync("ReceiveAction", request.Action, request.MovieId);

            return Ok(new { ok = true });
        }
    }
}
