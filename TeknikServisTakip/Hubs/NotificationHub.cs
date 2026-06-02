using Microsoft.AspNetCore.SignalR;

namespace TeknikServisTakip.Hubs
{
    public class NotificationHub : Hub
    {
        public async Task JoinAdminGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
        }

        public async Task JoinPersonelGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Personels");
        }

        public async Task SendToAdmin(string message, string type = "info")
        {
            await Clients.Group("Admins").SendAsync("ReceiveMessage", message, type);
        }

        public async Task SendToPersonel(string message, string type = "info")
        {
            await Clients.Group("Personels").SendAsync("ReceiveMessage", message, type);
        }

        public async Task SendToUser(string userId, string message, string type = "info")
        {
            await Clients.User(userId).SendAsync("ReceiveMessage", message, type);
        }


        // ========== EXCEL IMPORT PROGRESS METOTLAR ==========

        // İsteğe bağlı: Sadece import işlemini başlatan kullanıcıya (ConnectionId'ye özel) progress bar
        public async Task SendProgressToInfo(string connectionId, int percent, string message)
        {
            await Clients.Client(connectionId).SendAsync("ReceiveProgress", percent, message);
        }

   
        public async Task SendProgressToAll(int percent, string message)
        {
            await Clients.All.SendAsync("ReceiveProgress", percent, message);
        }

        // =================================================================


        public override async Task OnConnectedAsync()
        {
            if (Context.User?.IsInRole("Admin") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
            }
            else if (Context.User?.IsInRole("Personel") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Personels");
            }
            await base.OnConnectedAsync();
        }
    }
}