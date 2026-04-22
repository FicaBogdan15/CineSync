using CineSync.Hubs;
using CineSync.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using QRCoder;
using System.Security.Claims;

namespace CineSync.Controllers
{
    public class RemoteController : Controller
    {
        private readonly PairingService _pairing;
        private readonly IHubContext<RemoteHub> _hub;

        public RemoteController(PairingService pairing, IHubContext<RemoteHub> hub)
        {
            _pairing = pairing;
            _hub = hub;
        }

        [Authorize]
        public IActionResult Pair()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        public IActionResult GenerateQR([FromBody] GenerateQRRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ConnectionId))
            {
                return BadRequest(new { message = "ConnectionId is required." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var token = _pairing.CreateSession(req.ConnectionId, userId);

            var mobileUrl = Url.Action("Mobile", "Remote", new { token }, Request.Scheme);
            if (string.IsNullOrWhiteSpace(mobileUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to generate mobile pairing URL." });
            }

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(mobileUrl, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            var qrBytes = qrCode.GetGraphic(10);
            var qrBase64 = Convert.ToBase64String(qrBytes);

            return Json(new { token, qrBase64, mobileUrl });
        }

        public async Task<IActionResult> Mobile(string token)
        {
            var session = _pairing.GetSession(token);
            if (session == null)
            {
                return View("PairingExpired");
            }

            if (User.Identity?.IsAuthenticated != true)
            {
                var returnUrl = Url.Action("Mobile", "Remote", new { token });
                return RedirectToPage("/Account/Login", new { area = "Identity", returnUrl });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var userName = User.Identity?.Name ?? "Authenticated User";

            if (_pairing.MarkPaired(token, userId))
            {
                await _hub.Clients.Group($"desktop_{token}")
                    .SendAsync("PhonePaired", userName);
            }

            ViewBag.Token = token;
            ViewBag.IsPaired = session.IsPaired;
            return View();
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Action([FromBody] RemoteActionRequest req)
        {
            var session = _pairing.GetSession(req.Token);
            if (session == null || !session.IsPaired)
            {
                return Unauthorized();
            }

            await _hub.Clients.Group($"desktop_{req.Token}")
                .SendAsync("ReceiveAction", req.Action, req.MovieId);

            return Ok(new { ok = true });
        }
    }

    public record GenerateQRRequest(string ConnectionId);
    public record RemoteActionRequest(string Token, string Action, int MovieId);
}
