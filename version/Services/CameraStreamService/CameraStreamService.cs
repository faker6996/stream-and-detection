//using Microsoft.AspNetCore.SignalR;
//using Microsoft.Extensions.Options;
//using stream_multi_cam.Hubs;
//using stream_multi_cam.Models;
//using System.Diagnostics;
//using System.Text.Json;
//using FFMpegCore;
//using FFMpegCore.Pipes;
//using System.Drawing;
//using System.Drawing.Imaging;
//using stream_multi_cam.Services.BoundingBoxService;
//using stream_multi_cam.Services.CameraStreamService;

//public class CameraStreamServices : ICameraStreamService, IHostedService
//{
//    private readonly List<CameraConfig> _cams;
//    private readonly StreamingOptions _opt;
//    private readonly IWebHostEnvironment _env;
//    private readonly ILogger<CameraStreamService> _log;
//    private readonly IHubContext<OverlayHub> _hub;
//    private readonly IPCameraRoot _ipCameraRoot;
//    private readonly IBoundingBoxService _bboxService;
//    private readonly CancellationTokenSource _cts = new();

//    private readonly Dictionary<string, Process> _hlsProcs = new();
//    private readonly Dictionary<string, Process> _detectionProcs = new();
//    private static readonly HttpClient _client = new();

//    private static readonly JsonSerializerOptions _jsonOpt = new()
//    {
//        PropertyNameCaseInsensitive = true
//    };

//    public CameraStreamService(
//        IOptions<List<CameraConfig>> cams,
//        IOptions<IPCameraRoot> ipRoot,
//        IOptions<StreamingOptions> opt,
//        IWebHostEnvironment env,
//        IHubContext<OverlayHub> hub,
//        IBoundingBoxService bboxService,
//        ILogger<CameraStreamService> log)
//    {
//        _cams = cams.Value;
//        _hub = hub;
//        _opt = opt.Value;
//        _env = env;
//        _log = log;
//        _bboxService = bboxService;
//        _ipCameraRoot = ipRoot.Value;
//    }

//    public Task StartAsync(CancellationToken cancellationToken)
//    {
//        foreach (var ff in Process.GetProcessesByName("ffmpeg"))
//        {
//            try { ff.Kill(true); } catch { }
//        }

//        var hlsRoot = Path.Combine(_env.WebRootPath, "hls");
//        Directory.CreateDirectory(hlsRoot);

//        foreach (var cam in _cams)
//        {
//            var camDir = Path.Combine(hlsRoot, cam.CameraId);
//            Directory.CreateDirectory(camDir);

//            // Xóa file cũ...
//            foreach (var f in Directory.EnumerateFiles(camDir, "*.*"))
//            {
//                try { File.Delete(f); } catch { }
//            }

//            // Khởi chạy 2 luồng song song cho mỗi camera
//            _ = RunHlsLoopAsync(cam, camDir, _cts.Token);
//            _ = RunDetectionLoopAsync(cam, _cts.Token);
//        }

//        return Task.CompletedTask;
//    }

//    // LUỒNG 1: CHỈ TẠO VIDEO HLS
//    // Trong file CameraStreamService.cs
//    private async Task RunHlsLoopAsync(CameraConfig cam, string dir, CancellationToken ct)
//    {
//        var playlist = Path.Combine(dir, "index.m3u8");
//        var rtspUrl = BuildRtspUrl(cam);
//        var backoff = TimeSpan.FromSeconds(5);

//        while (!ct.IsCancellationRequested)
//        {
//            if (_hlsProcs.TryGetValue(cam.CameraId, out var oldProc) && !oldProc.HasExited)
//            {
//                try { oldProc.Kill(true); } catch { }
//            }

//            var copy = _opt.CopyCodec ? "-c:v copy" : "-c:v libx264 -preset veryfast -tune zerolatency";
//            var llhls = _opt.EnableLLHls
//                ? "-hls_part_size 0.2 -hls_flags delete_segments+independent_segments -hls_fmp4_init_filename init.mp4 -hls_segment_type fmp4"
//                : "-hls_flags delete_segments";

//            // =================================================================
//            // == BỔ SUNG LẠI THAM SỐ GOP (KEYFRAME) ==
//            // Buộc tạo keyframe mỗi `cam.Fps` khung hình (tương đương 1 giây nếu Fps đúng)
//            // Điều này giúp việc cắt segment HLS ổn định hơn.
//            var gop = cam.Fps > 0 ? $"-g {cam.Fps} -keyint_min {cam.Fps}" : "";
//            // =================================================================

//            // Thêm biến {gop} vào chuỗi args
//            var args = $"-hide_banner -loglevel error -rtsp_transport tcp -i \"{rtspUrl}\" -map 0:v {copy} -an {gop} -f hls -hls_time {_opt.HlsTime} -hls_list_size {_opt.HlsListSize} {llhls} -muxdelay 0 -hls_segment_filename \"{Path.Combine(dir, "seg_%d.ts")}\" -y \"{playlist}\"";

//            _log.LogInformation("[{camId}] Starting HLS process with arguments: {args}", cam.CameraId, args);

//            var proc = Process.Start(new ProcessStartInfo
//            {
//                FileName = _opt.FfmpegPath,
//                Arguments = args,
//                UseShellExecute = false,
//                CreateNoWindow = true,
//                RedirectStandardError = true
//            })!;

//            _hlsProcs[cam.CameraId] = proc;
//            _log.LogInformation("[{camId}] HLS streaming process started with PID {pid}.", cam.CameraId, proc.Id);

//            // Task đọc lỗi từ FFmpeg (giữ nguyên)
//            _ = Task.Run(async () =>
//            {
//                while (!proc.StandardError.EndOfStream)
//                {
//                    var line = await proc.StandardError.ReadLineAsync();
//                    if (!string.IsNullOrEmpty(line) && line.Contains("error", StringComparison.OrdinalIgnoreCase))
//                    {
//                        _log.LogWarning("[{camId}-HLS-ERROR] {message}", cam.CameraId, line);
//                    }
//                }
//            }, ct);


//            await proc.WaitForExitAsync(ct);
//            if (ct.IsCancellationRequested) break;

//            _log.LogWarning("[{camId}] HLS process exited with code {exitCode}. Restarting...", cam.CameraId, proc.ExitCode);
//            await Task.Delay(backoff, ct);
//        }
//    }
//    // LUỒNG 2: CHỈ XỬ LÝ FRAME ĐỂ DETECTION
//    private async Task RunDetectionLoopAsync(CameraConfig cam, CancellationToken ct)
//    {
//        // LOG THÊM: In ra URL RTSP cho luồng phụ để kiểm tra
//        var rtspUrl = BuildRtspUrl(cam);
//        _log.LogInformation("[{camId}] Constructed detection RTSP URL: {url}", cam.CameraId, rtspUrl);

//        var backoff = TimeSpan.FromSeconds(5);
//        var stopwatch = new Stopwatch();

//        while (!ct.IsCancellationRequested)
//        {
//            if (_detectionProcs.TryGetValue(cam.CameraId, out var oldProc) && !oldProc.HasExited)
//            {
//                try { oldProc.Kill(true); } catch { }
//            }

//            var detectionArgs = $"-i \"{rtspUrl}\" -r 5 -q:v 2 -f image2pipe -";

//            // LOG THÊM: In ra toàn bộ câu lệnh sẽ chạy để dễ debug
//            _log.LogDebug("[{camId}] Full detection command: {ffmpeg} {args}", cam.CameraId, _opt.FfmpegPath, detectionArgs);

//            var psi = new ProcessStartInfo
//            {
//                FileName = _opt.FfmpegPath,
//                Arguments = detectionArgs,
//                RedirectStandardOutput = true,
//                RedirectStandardError = true, // Thêm dòng này để đọc log lỗi từ FFmpeg
//                UseShellExecute = false,
//                CreateNoWindow = true
//            };

//            using var proc = Process.Start(psi)!;
//            _detectionProcs[cam.CameraId] = proc;
//            _log.LogInformation("[{camId}] Detection process started with PID {pid}.", cam.CameraId, proc.Id);
//            stopwatch.Restart();

//            // LOG THÊM: Tạo task đọc lỗi từ FFmpeg để không bỏ sót thông tin
//            _ = Task.Run(async () =>
//            {
//                while (!proc.StandardError.EndOfStream)
//                {
//                    var line = await proc.StandardError.ReadLineAsync();
//                    if (!string.IsNullOrEmpty(line) && line.Contains("error", StringComparison.OrdinalIgnoreCase))
//                    {
//                        _log.LogWarning("[{camId}-DETECT-ERROR] {message}", cam.CameraId, line);
//                    }
//                }
//            }, ct);


//            try
//            {
//                await ReadJpegStreamAsync(proc.StandardOutput.BaseStream, async (imageBytes) =>
//                {
//                    var timestamp = stopwatch.Elapsed;

//                    //try
//                    //{
//                    //    string debugDir = "debug_images";
//                    //    Directory.CreateDirectory(debugDir); // Đảm bảo thư mục tồn tại
//                    //    string imagePath = Path.Combine(debugDir, $"{cam.CameraId}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg");
//                    //    await File.WriteAllBytesAsync(imagePath, imageBytes, ct);
//                    //    _log.LogInformation("Saved debug image for {camId} to {path}", cam.CameraId, imagePath);
//                    //}
//                    //catch (Exception ex)
//                    //{
//                    //    _log.LogError(ex, "Failed to save debug image for {camId}", cam.CameraId);
//                    //}

//                    var boxes = await CallApiGetBoxesFromBytes(imageBytes, cam.CameraId, ct);
//                    if (boxes.Count > 0)
//                    {
//                        _bboxService.UpdateBoxes(cam.CameraId, timestamp, boxes);
//                    }
//                }, ct);
//            }
//            catch (OperationCanceledException) { break; }
//            catch (Exception ex)
//            {
//                _log.LogError(ex, "[{camId}] Error reading detection stream.", cam.CameraId);
//            }

//            await proc.WaitForExitAsync(ct);
//            if (ct.IsCancellationRequested) break;

//            // LOG THÊM: Ghi lại ExitCode khi tiến trình kết thúc. Đây là thông tin rất quan trọng.
//            _log.LogWarning("[{camId}] Detection process PID {pid} exited with code {exitCode}. Restarting...", cam.CameraId, proc.Id, proc.ExitCode);

//            await Task.Delay(backoff, ct);
//        }
//    }
//    // Hàm đọc từng ảnh JPEG từ một stream liên tục
//    private async Task ReadJpegStreamAsync(Stream stream, Func<byte[], Task> onImage, CancellationToken ct)
//    {
//        byte[] jpegHeader = { 0xff, 0xd8 };
//        byte[] jpegFooter = { 0xff, 0xd9 };
//        var buffer = new List<byte>(256 * 1024); // 256KB buffer
//        var readBuffer = new byte[4096];
//        int bytesRead;

//        while ((bytesRead = await stream.ReadAsync(readBuffer, 0, readBuffer.Length, ct)) > 0)
//        {
//            buffer.AddRange(readBuffer.Take(bytesRead));
//            while (true)
//            {
//                int startIndex = buffer.IndexOf(jpegHeader);
//                if (startIndex == -1) break;

//                int endIndex = buffer.IndexOf(jpegFooter, startIndex);
//                if (endIndex == -1) break;

//                endIndex += jpegFooter.Length;
//                var imageBytes = buffer.GetRange(startIndex, endIndex - startIndex).ToArray();
//                await onImage(imageBytes);

//                buffer.RemoveRange(0, endIndex);
//            }
//        }
//    }

//    // Hàm gọi API mới, nhận vào byte array
//    private async Task<List<List<int>>> CallApiGetBoxesFromBytes(byte[] imageBytes, string camId, CancellationToken ct)
//    {
//        // LOG THÊM: Xác nhận hàm được gọi
//        _log.LogInformation("[{camId}] Preparing to call detection API with image size: {size} bytes.", camId, imageBytes.Length);

//        using var content = new MultipartFormDataContent { { new ByteArrayContent(imageBytes), "image", "snapshot.jpg" } };
//        try
//        {
//            var resp = await _client.PostAsync(_opt.UrlModel, content, ct);

//            // LOG THÊM: Luôn ghi lại status code trả về
//            _log.LogInformation("[{camId}] API responded with status code: {statusCode}", camId, resp.StatusCode);

//            if (!resp.IsSuccessStatusCode)
//            {
//                // LOG THÊM: Nếu lỗi, đọc và ghi lại nội dung lỗi từ API
//                var errorBody = await resp.Content.ReadAsStringAsync(ct);
//                _log.LogWarning("[{camId}] API call failed. Response body: {errorBody}", camId, errorBody);
//                return new List<List<int>>();
//            }

//            // LOG THÊM: Ghi lại nội dung JSON gốc trước khi phân tích
//            var json = await resp.Content.ReadAsStringAsync(ct);
//            _log.LogDebug("[{camId}] API successful response JSON: {json}", camId, json);

//            var result = JsonSerializer.Deserialize<DetectionResult>(json, _jsonOpt);
//            var boxes = result?.Data?.FirstOrDefault()?.Boxes ?? new();

//            // LOG THÊM: Ghi lại số lượng box nhận được
//            _log.LogInformation("[{camId}] ✅ API returned {count} boxes.", camId, boxes.Count);

//            return boxes;
//        }
//        catch (Exception ex)
//        {
//            _log.LogError(ex, "❌ Unhandled exception during API call for {Cam}", camId);
//            return new List<List<int>>();
//        }
//    }
//    private string BuildRtspUrl(CameraConfig cam, bool useSubstream = false)
//    {
//        var channel = cam.Channel ?? 1;
//        // Nếu useSubstream là true, dùng subtype=1, ngược lại dùng subtype=0
//        var subtype = useSubstream ? 1 : (cam.subtype ?? 0);
//        return $"{_ipCameraRoot.Url}?channel={channel}&subtype={subtype}";
//    }

//    public Task StopAsync(CancellationToken cancellationToken)
//    {
//        _cts.Cancel();
//        var allProcs = _hlsProcs.Values.Concat(_detectionProcs.Values);
//        foreach (var proc in allProcs)
//        {
//            try { if (!proc.HasExited) proc.Kill(true); } catch { }
//        }
//        return Task.CompletedTask;
//    }
//}


//// Extension method tiện ích để tìm mảng byte
//public static class BufferExtensions
//{
//    public static int IndexOf(this List<byte> buffer, byte[] pattern, int startIndex = 0)
//    {
//        for (int i = startIndex; i <= buffer.Count - pattern.Length; i++)
//        {
//            bool found = true;
//            for (int j = 0; j < pattern.Length; j++)
//            {
//                if (buffer[i + j] != pattern[j])
//                {
//                    found = false;
//                    break;
//                }
//            }
//            if (found) return i;
//        }
//        return -1;
//    }
//}