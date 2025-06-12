stream_multi_camera/               # Th? m?c g?c d? án
??? Controllers/                   # API Controllers
?   ??? BoundingBoxController.cs   # Nh?n d? li?u bounding box t? bên th? ba
?
??? Hubs/                          # SignalR Hubs
?   ??? OverlayHub.cs              # Hub ??y d? li?u overlay t?i client
?
??? Models/                        # Các l?p d? li?u dùng chung
?   ??? BoundingBox.cs             # ??i di?n m?t box (x,y,w,h, label)
?   ??? CameraConfig.cs            # C?u hình camera (ID, RTSP URL, các tham s?)
?
??? Services/                      # Business Logic / Background tasks
?   ??? CameraStreamService/       # X? lý stream t? camera
?   ?   ??? ICameraStreamService.cs
?   ?   ??? CameraStreamService.cs
?   ??? BoundingBoxService/        # Qu?n lý d? li?u bounding box
?       ??? IBoundingBoxService.cs
?       ??? BoundingBoxService.cs
?
??? Pages/                         # Razor Pages cho giao di?n Blazor
?   ??? Index.razor                # Trang chính hi?n th? 4 camera
?   ??? VideoFeed.razor            # Component hi?n th? m?t camera + overlay
?
??? Shared/                        # Layout và component chung
?   ??? MainLayout.razor           # Layout dùng chung cho toàn ?ng d?ng
?
??? wwwroot/                       # Tài nguyên t?nh (JS, CSS, th? vi?n)
?   ??? js/
?   ?   ??? overlay.js             # Script h? tr? v? canvas overlay
?   ??? lib/
?       ??? hls.js                 # Th? vi?n HLS.js ?? phát stream HLS
?
??? appsettings.json               # C?u hình app (cameras, SignalR, v.v.)
??? Program.cs                     # Entry point + c?u hình DI, SignalR, các Service
??? stream_multi_camera.csproj     # D? án .NET
??? Dockerfile                     # (N?u deploy container) c?u hình Docker build
??? .gitignore                     # Lo?i tr? file th? m?c không c?n commit

 ┌── FFmpeg (HLS, 1 s/seg) ─────────┐
 │ tạo seg_801.ts ─────►            │
 │                    Watcher       │
 │                    snapshot + AI │
 │                    (≈300 ms)     │
 └────────────────────┬─────────────┘
                      │   SignalR:  {cam:"03", sn:801, boxes:[…]}
                      ▼
                Back-End Hub
                      │
          (≈ < 50 ms) ▼
   Front-End overlay.js
   ┌────────────┐
   │ Hls.js     │  FRAG_CHANGED → sn=801
   └────────────┘
        │   lấy box sn=801  → vẽ
        ▼
   <canvas>
