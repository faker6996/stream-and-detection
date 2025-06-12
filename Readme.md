# Stream & Detection

## Multi‑Camera Surveillance with Real‑Time AI Overlays (ASP.NET Core + Blazor)

**stream\_multi\_cam** is an end‑to‑end web surveillance stack that lets you watch many IP cameras and paint AI‑powered bounding boxes on top of the video—live and in the browser.

|  Key Capability             |  What it does                                                                                                                 |
| --------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
|  **Multi‑Camera Dashboard** | One Blazor page shows every camera feed at once.                                                                              |
|  **RTSP → HLS**             | Each RTSP stream is transcoded to HLS segments (1 s) with **FFmpeg** so all modern browsers can play it.                      |
|  **Real‑Time Detection**    |  The first frame of every segment is sent to your AI API (e.g., YOLO).                                                        |
|  **Instant Overlays**       | Bounding‑box data is pushed to the browser via **SignalR** and drawn on an HTML Canvas that sits on top of the video element. |
|  **Config‑Driven**          |  Add cameras or tweak FFmpeg/HLS/A I in `appsettings.json`—no code changes.                                                   |

---

## 1 · Architecture

```text
┌────────────────────────────────────────────────────────────┐
│                  Back‑End (ASP.NET Core)                   │
│                                                            │
│  RTSP ──▶ [FFmpeg HLS] ──▶ /wwwroot/hls/seg_###.ts          │
│                 ▲  FileSystemWatcher                       │
│                 │                                         │
│        snapshot + AI call  ──▶  boxes                      │
│                 │                                         │
│  SignalR Hub  ──▶  ReceiveBoxes                            │
└───────┬────────────────────────────────────────────────────┘
        │ WebSocket
┌───────▼────────────────────────────────────────────────────┐
│              Front‑End (Blazor + HLS.js)                  │
│                                                            │
│ Hls.js player ◀── HTTP GET HLS segments                    │
│        │                                                   │
│   FRAG_CHANGED → drawBoxes() on canvas                     │
└────────────────────────────────────────────────────────────┘
```

---

## 2 · Folder Layout

```
stream_multi_cam/
├── Controllers/                 # optional REST endpoints
├── Hubs/                        # OverlayHub.cs (SignalR)
├── Models/                      # BoundingBox.cs, CameraConfig.cs, …
├── Pages/                       # Index.razor, VideoFeed.razor, …
├── Services/
│   ├── CameraStreamService/     # RTSP → HLS, snapshot + AI
│   └── BoundingBoxService/      # Store & broadcast boxes
├── wwwroot/
│   ├── js/overlay.js            # Hls.js + SignalR + canvas overlay
│   └── lib/hls.js               # Third‑party scripts
├── appsettings.json             # All runtime config
└── Program.cs                   # DI & middleware setup
```

---

## 3 · Quick Start

\### Prerequisites

| Tool                    | Notes                                                                          |
| ----------------------- | ------------------------------------------------------------------------------ |
| **.NET 6 SDK or newer** | [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download) |
| **FFmpeg**              | Install or set an absolute path in `Streaming:FfmpegPath`.                     |
| **AI Detection API**    | Any HTTP endpoint that returns bounding boxes.                                 |

\### Configuration (`appsettings.json`)

```jsonc
{
  "Streaming": {
    "FfmpegPath": "C:/ffmpeg/bin/ffmpeg.exe",
    "UrlModel":   "http://ai-server/api/detect",
    "HlsTime":    1,
    "HlsListSize": 3
  },
  "IPCameraRoot": {
    "Url": "rtsp://user:pass@192.168.1.10/Streaming/Channels"
  },
  "Cameras": [
    { "CameraId": "01", "Channel": 1, "subtype": 0, "Location": "Lobby" },
    { "CameraId": "02", "Channel": 2, "subtype": 0, "Location": "Parking" }
  ]
}
```

\### Run

```bash
cd stream_multi_cam
dotnet run
# browse to https://localhost:5001 (or the port shown in console)
```

---

## 4 · Tech Stack

| Layer         | Technology                        |
| ------------- | --------------------------------- |
| **Backend**   | ASP.NET Core 6 + Blazor Server    |
| **Streaming** | FFmpeg, HLS (HTTP Live Streaming) |
| **Realtime**  | SignalR                           |
| **Frontend**  | HLS.js, HTML 5 Canvas, vanilla JS |
| **Languages** | C#, JavaScript                    |

---

## 5 · Tuning & Extending

* **Add more cameras** – just append to the `Cameras` array.
* **Swap AI model** – point `Streaming:UrlModel` to your own endpoint.
* **Lower latency** – enable LL‑HLS (`-hls_part_size 0.2`) and set `lowLatencyMode: true` in `overlay.js`.

---

Made with ☕ + 💻 by Your Team.
