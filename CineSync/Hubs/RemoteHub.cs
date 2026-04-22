using Microsoft.AspNetCore.SignalR;

namespace CineSync.Hubs
{
    public class RemoteHub : Hub
    {
        public async Task RegisterDesktop(string pairingToken)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"desktop_{pairingToken}");
            await Clients.Caller.SendAsync("DesktopRegistered", pairingToken);
        }

        public async Task SendAction(string pairingToken, string action, int movieId)
        {
            await Clients.Group($"desktop_{pairingToken}")
                .SendAsync("ReceiveAction", action, movieId);
        }

        public async Task NotifyPaired(string pairingToken, string userName)
        {
            await Clients.Group($"desktop_{pairingToken}")
                .SendAsync("PhonePaired", userName);
        }
    }
}
