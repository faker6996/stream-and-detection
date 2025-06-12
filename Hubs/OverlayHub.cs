using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace stream_multi_cam.Hubs
{
    public class OverlayHub : Hub
    {
        // Cho client tham gia group tương ứng với cameraId
        public Task JoinCameraGroup(string cameraId)
        {
            return Groups.AddToGroupAsync(Context.ConnectionId, cameraId);
        }

        // (Tùy chọn) Cho client rời group
        public Task LeaveCameraGroup(string cameraId)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, cameraId);
        }

        public override Task OnConnectedAsync()
        {
            Console.WriteLine($"🟢 Client connected: {Context.ConnectionId}");
            return base.OnConnectedAsync();
        }
    }
}
