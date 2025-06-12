//using Microsoft.AspNetCore.SignalR;
//using Microsoft.Extensions.Options;
//using stream_multi_cam.Hubs;
//using stream_multi_cam.Models;

//namespace stream_multi_cam.Services.BoundingBoxService
//{
//    public class BoundingBoxServices : IBoundingBoxService
//    {
//        private readonly IHubContext<OverlayHub> _hubContext;
//        private readonly ILogger<BoundingBoxService> _logger;
//        // === SỬA LỖI: Thêm danh sách cấu hình camera ===
//        private readonly List<CameraConfig> _cameraConfigs;

//        // === SỬA LỖI: Cập nhật Constructor để nhận cấu hình ===
//        public BoundingBoxService(
//            IHubContext<OverlayHub> hubContext,
//            ILogger<BoundingBoxService> logger,
//            IOptions<List<CameraConfig>> cameraConfigs) // Thêm tham số này
//        {
//            _hubContext = hubContext;
//            _logger = logger;
//            _cameraConfigs = cameraConfigs.Value; // Gán giá trị
//        }

//        // === SỬA LỖI: Cập nhật hàm UpdateBoxes để sử dụng cấu hình ===
//        public void UpdateBoxes(string cameraId, TimeSpan timestamp, List<List<int>> rawBoxes)
//        {

//            var boxList = rawBoxes
//                .Select(b => new {
//                    X = (float)b[0] ,
//                    Y = (float)b[1],
//                    Width = (float)(b[2] - b[0]),
//                    Height = (float)(b[3] - b[1]),
//                    Label = "person"
//                })
//                .ToList();

//            if (boxList.Any())
//            {
//                _logger.LogInformation("[{cameraId}] Sending {count} scaled boxes with timestamp {ts} to SignalR group.", cameraId, boxList.Count, timestamp.TotalMilliseconds);
//                _hubContext.Clients
//                    .Group(cameraId)
//                    .SendAsync("ReceiveBoxes", cameraId, timestamp.TotalMilliseconds, boxList);
//            }
//        }
//    }
//}