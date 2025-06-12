# Stream & Detection: Hệ thống giám sát đa camera với Lớp phủ AI theo thời gian thực

Dự án này là một ứng dụng web mạnh mẽ được xây dựng bằng ASP.NET Core và Blazor, cho phép hiển thị và xử lý video từ nhiều camera IP đồng thời. Hệ thống chuyển đổi các luồng RTSP thành định dạng HLS để có thể phát trên trình duyệt, đồng thời tích hợp một dịch vụ AI để nhận diện đối tượng và vẽ các bounding box (hộp giới hạn) lên video theo thời gian thực bằng SignalR.

## Tính năng chính

-   **Giám sát đa camera:** Hiển thị đồng thời nhiều luồng video từ các camera IP khác nhau trên một giao diện dashboard duy nhất.
-   **Streaming HLS:** Sử dụng FFmpeg để chuyển đổi các luồng RTSP từ camera thành định dạng HLS (HTTP Live Streaming), tương thích với hầu hết các trình duyệt hiện đại.
-   **Nhận diện đối tượng thời gian thực:** Trích xuất các khung hình từ luồng video, gửi đến một API bên ngoài để thực hiện nhận diện đối tượng (ví dụ: phát hiện người).
-   **Lớp phủ (Overlay) thời gian thực:** Sử dụng SignalR để đẩy tọa độ các bounding box từ backend về client ngay lập tức, và dùng Javascript để vẽ chúng lên trên lớp video tương ứng.
-   **Kiến trúc linh hoạt:** Dễ dàng cấu hình và mở rộng thêm camera thông qua file `appsettings.json`.

## Kiến trúc hệ thống

Hệ thống được xây dựng dựa trên kiến trúc client-server với sự tương tác mạnh mẽ giữa backend, frontend và một dịch vụ AI bên ngoài.

### Luồng hoạt động

Luồng xử lý dữ liệu từ camera đến khi hiển thị bounding box trên màn hình người dùng diễn ra như sau:


                                  +---------------------------------------------+
                                  |             Back-End (.NET Core)            |
                                  |                                             |

[Camera RTSP] ---> [FFmpeg HLS Process] --(tạo file)--> [wwwroot/hls/seg_xxx.ts]     |
|                     |                       |
|        (FileSystemWatcher phát hiện)        |
|                     |                       |
|                     v                       |
| [Lấy ảnh từ .ts] -> [Gọi API AI] -> [Boxes] |
|                     |                       |
|      (SignalR Hub gửi tin nhắn)            |
+---------------------|-----------------------+
|
v (WebSocket)
+---------------------------------------------+
|           Front-End (Blazor & JS)           |
|                                             |
[Trình duyệt] <---- [HLS.js Player] <---(tải file)----- [wwwroot/hls/seg_xxx.ts]     |
|                     ^                       |
|                     | (Vẽ lên canvas)       |
|                     |                       |
| [overlay.js] <----(nhận)---- [Boxes]         |
|                                             |
+---------------------------------------------+


### Cấu trúc thư mục


stream_multi_cam/
│
├── Controllers/
│   └── BoundingBoxController.cs      # (Tùy chọn) API để nhận dữ liệu từ bên ngoài
│
├── Hubs/
│   └── OverlayHub.cs                 # SignalR Hub để giao tiếp real-time với client
│
├── Models/
│   ├── BoundingBox.cs                # Model cho một bounding box
│   └── CameraConfig.cs               # Model cho cấu hình một camera
│
├── Pages/
│   ├── Index.razor                   # Trang dashboard chính hiển thị các camera
│   └── VideoFeed.razor               # Component con cho mỗi ô video và canvas
│
├── Services/
│   ├── CameraStreamService/          # Logic chính xử lý luồng video
│   │   ├── ICameraStreamService.cs
│   │   └── CameraStreamService.cs    # Lấy RTSP, chạy FFmpeg, xử lý segment
│   └── BoundingBoxService/           # Logic quản lý và đẩy dữ liệu box
│       ├── IBoundingBoxService.cs
│       └── BoundingBoxService.cs
│
├── wwwroot/
│   ├── js/
│   │   └── overlay.js                # Javascript để vẽ box và quản lý player
│   └── lib/
│       └── hls.js                    # Thư viện HLS.js
│
├── appsettings.json                  # File cấu hình trung tâm
├── Program.cs                        # Entry point, đăng ký các dịch vụ
├── .gitignore                        # Loại trừ các file không cần thiết khỏi source control
└── stream_multi_cam.csproj           # File dự án .NET


## Hướng dẫn cài đặt và sử dụng

### Yêu cầu
1.  **[.NET SDK](https://dotnet.microsoft.com/download)** (phiên bản 6.0 hoặc mới hơn).
2.  **[FFmpeg](https://ffmpeg.org/download.html)**: Phải được cài đặt và có thể truy cập được từ dòng lệnh, hoặc chỉ định đường dẫn tuyệt đối trong `appsettings.json`.
3.  Một dịch vụ AI nhận diện đối tượng có thể truy cập qua HTTP.

### Cấu hình
Mở file `appsettings.json` và chỉnh sửa các thông số sau:

-   **`Streaming`**:
    -   `UrlModel`: Địa chỉ URL của API AI.
    -   `FfmpegPath`: Đường dẫn đến file `ffmpeg.exe`.
    -   Các tham số khác cho HLS (`HlsTime`, `HlsListSize`...).
-   **`IPCameraRoot`**:
    -   `Url`: URL gốc của NVR hoặc camera.
-   **`Cameras`**:
    -   Thêm hoặc sửa thông tin cho từng camera, bao gồm `CameraId`, `Channel`, `subtype`...

### Chạy ứng dụng
1.  Mở project bằng Visual Studio hoặc dùng dòng lệnh.
2.  Chạy lệnh: `dotnet run`
3.  Truy cập vào địa chỉ `https://localhost:<port>` được hiển thị trên console.

## Công nghệ sử dụng
-   **Backend:** ASP.NET Core 6+, Blazor Server
-   **Real-time:** SignalR
-   **Streaming:** FFmpeg, HLS (HTTP Live Streaming)
-   **Frontend:** Javascript, HLS.js, HTML5 Canvas
-   **Ngôn ngữ:** C#, Javascript
