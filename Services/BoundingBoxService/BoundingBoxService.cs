using Microsoft.AspNetCore.SignalR;
using stream_multi_cam.Hubs;
using stream_multi_cam.Models;
using System.Collections.Concurrent;

namespace stream_multi_cam.Services.BoundingBoxService
{
    public class BoundingBoxService : IBoundingBoxService
    {
        private readonly IHubContext<OverlayHub> _hubContext;
        private readonly ConcurrentDictionary<string, IEnumerable<BoundingBox>> _store = new();

        public BoundingBoxService(IHubContext<OverlayHub> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// Cập nhật bounding boxes từ API detection trả về (dạng [x1, y1, x2, y2])
        /// </summary>
        public void UpdateBoxes(string cameraId, int sn, List<List<int>> rawBoxes)
        {
            var boxList = rawBoxes
                .Where(b => b.Count == 4)
                .Select(b => new BoundingBox(
                    X: b[0],
                    Y: b[1],
                    Width: b[2] - b[0],
                    Height: b[3] - b[1],
                    Label: "person"))
                .ToList();

            // Có thể lưu theo sn nếu muốn cache nhiều segment, còn không chỉ lưu mới nhất
            _store[cameraId] = boxList;

            _hubContext.Clients
                .Group(cameraId)
                .SendAsync("ReceiveBoxes", cameraId, sn, boxList);
        }
    }
}