using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using stream_multi_cam.Hubs;
using stream_multi_cam.Models;
using stream_multi_cam.Services.BoundingBoxService;
using stream_multi_cam.Services.CameraStreamService;
using System.Diagnostics;
using System.Text.Json;

public class CameraStreamService : ICameraStreamService, IHostedService
{
    private readonly List<CameraConfig> _cams;
    private readonly StreamingOptions _opt;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CameraStreamService> _log;
    private readonly Dictionary<string, Process> _procs = new();
    private readonly IPCameraRoot _ipCameraRoot;
    private readonly IBoundingBoxService _bboxService;
    private static readonly HttpClient _client = new();
    private static readonly TimeSpan DetectionInterval = TimeSpan.FromSeconds(2);
    private readonly IHubContext<OverlayHub> _hub;
    private static readonly JsonSerializerOptions _jsonOpt = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CameraStreamService(
        IOptions<List<CameraConfig>> cams,
        IOptions<IPCameraRoot> ipRoot,
        IOptions<StreamingOptions> opt,
        IWebHostEnvironment env,
        IHubContext<OverlayHub> hub,
        IBoundingBoxService bboxService,
        ILogger<CameraStreamService> log)
    {
        _cams = cams.Value;
        _hub = hub;
        _opt = opt.Value;
        _env = env;
        _log = log;
        _bboxService = bboxService;
        _ipCameraRoot = ipRoot.Value;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        // 1. Kill mọi ffmpeg & đợi thoát
        foreach (var ff in Process.GetProcessesByName("ffmpeg"))
        {
            try { ff.Kill(true); ff.WaitForExit(3000); } catch { }
        }

        var hlsRoot = Path.Combine(_env.WebRootPath, "hls");
        Directory.CreateDirectory(hlsRoot);

        foreach (var cam in _cams)
        {
            var camDir = Path.Combine(hlsRoot, cam.CameraId);
            Directory.CreateDirectory(camDir);

            // 2. Xoá file cũ – retry nếu đang bị khoá
            foreach (var f in Directory.EnumerateFiles(camDir, "*.*"))
            {
                for (int i = 0; i < 3; i++)
                {
                    try { File.Delete(f); break; }
                    catch (IOException) { await Task.Delay(300, ct); }
                }
            }

            _ = RunFfmpegLoopAsync(cam, camDir, ct);
        }
    }


    public Task StopAsync(CancellationToken ct)
    {
        foreach (var kvp in _procs.ToList())
        {
            try { if (!kvp.Value.HasExited) kvp.Value.Kill(entireProcessTree: true); } catch { }
        }
        _procs.Clear();
        return Task.CompletedTask;
    }

    private async Task RunFfmpegLoopAsync(CameraConfig cam, string dir, CancellationToken ct)
    {
        var playlist = Path.Combine(dir, "index.m3u8");
        var backoff = TimeSpan.FromSeconds(3);

        while (!ct.IsCancellationRequested)
        {
            if (_procs.TryGetValue(cam.CameraId, out var oldProc) && !oldProc.HasExited)
            {
                try { oldProc.Kill(entireProcessTree: true); oldProc.Dispose(); } catch { }
            }

            var args = BuildArgs(cam, dir, playlist);
            var psi = new ProcessStartInfo
            {
                FileName = _opt.FfmpegPath,
                Arguments = args,
                WorkingDirectory = dir,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi)!;
            _procs[cam.CameraId] = proc;

            // FFmpeg stderr reader
            _ = Task.Run(async () =>
            {
                while (!proc.StandardError.EndOfStream && !_opt.DebugFfmpeg)
                {
                    var line = await proc.StandardError.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line) && line.Contains("error", StringComparison.OrdinalIgnoreCase))
                        _log.LogWarning("[{cam}] FFmpeg: {msg}", cam.CameraId, line);
                }
            }, ct);

            var watcher = new FileSystemWatcher(dir, "seg_*.ts")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime
            };

            watcher.Created += async (_, e) =>
            {
                // seg_012.ts  → 12
                if (!int.TryParse(Path.GetFileNameWithoutExtension(e.Name)[4..], out int sn))
                    return;

                await ProcessSegmentAsync(cam, sn, e.FullPath, ct);
            };

            watcher.EnableRaisingEvents = true;

            await proc.WaitForExitAsync(ct);

            watcher.Dispose();

            if (ct.IsCancellationRequested) break;
            await Task.Delay(backoff, ct);
            backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 15));
        }
    }

    private string BuildArgs(CameraConfig cam, string dir, string playlist)
    {
        string rtspUrl = BuildRtspUrl(_ipCameraRoot, cam);
        var copy = _opt.CopyCodec ? "-c:v copy" : "-c:v libx264 -preset veryfast -tune zerolatency";
        var gop = cam.Fps > 0 ? $"-g {cam.Fps} -keyint_min {cam.Fps}" : "";
        var llhls = _opt.EnableLLHls
            ? "-hls_part_size 0.2 -hls_flags delete_segments+independent_segments -hls_fmp4_init_filename init.mp4 -hls_segment_type fmp4"
            : "-hls_flags delete_segments";

        return $"-hide_banner -loglevel {(_opt.DebugFfmpeg ? "verbose" : "warning")} -rtsp_transport tcp -i \"{rtspUrl}\" {copy} -an {gop} -f hls -hls_time {_opt.HlsTime} -hls_list_size {_opt.HlsListSize} {llhls} -hls_segment_filename \"{Path.Combine(dir, "seg_%03d.ts")}\" -y \"{playlist}\"";
    }

    private string BuildRtspUrl(IPCameraRoot root, CameraConfig cam)
    {
        var channel = cam.Channel ?? 1;
        var subtype = cam.subtype ?? 0;
        return $"{root.Url}?channel={channel}&subtype={subtype}";
    }

    private async Task ProcessSegmentAsync(
        CameraConfig cam, int sn, string segPath, CancellationToken ct)
    {
        try
        {
            // Snapshot ra JPG cạnh segment
            string jpg = Path.ChangeExtension(segPath, ".jpg");
            var snap = Process.Start(new ProcessStartInfo
            {
                FileName = _opt.FfmpegPath,
                Arguments = $"-y -i \"{segPath}\" -frames:v 1 -q:v 2 \"{jpg}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            });
            await snap.WaitForExitAsync(ct);
            if (snap.ExitCode != 0) return;

            var boxes = await CallApiGetBoxs(jpg, _opt, cam.CameraId, _log, ct);
            if (boxes.Count == 0) return;

            _bboxService.UpdateBoxes(cam.CameraId, sn, boxes);   // 👈 thêm sn
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[{Cam}] detect segment {Sn} fail", cam.CameraId, sn);
        }
    }


    private static async Task<List<List<int>>> CallApiGetBoxs(
            string snapshotPath,
            StreamingOptions otp,
            string camId,
            ILogger log,
            CancellationToken ct = default)
    {
        // 1) Kiểm tra file tồn tại
        if (!File.Exists(snapshotPath))
        {
            log.LogDebug("[{Cam}] Snapshot chưa có: {Path}", camId, snapshotPath);
            return new();
        }

        // 2) Chuẩn bị multipart (FileShare.ReadWrite để không khóa tệp)
        await using var fs = new FileStream(
            snapshotPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);

        using var content = new MultipartFormDataContent
        {
            { new StreamContent(fs), "image", Path.GetFileName(snapshotPath) }
        };

        var sw = Stopwatch.StartNew();
        try
        {
            // 3) Gửi POST
            var resp = await _client.PostAsync(otp.UrlModel, content, ct);

            if (!resp.IsSuccessStatusCode)
            {
                log.LogWarning("[{Cam}] Detect API {Status} ({Ms} ms)",
                               camId, (int)resp.StatusCode, sw.ElapsedMilliseconds);
                return new();
            }

            // 4) Đọc JSON
            var json = await resp.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<DetectionResult>(json, _jsonOpt);
            var boxes = result?.Data?.FirstOrDefault()?.Boxes ?? new();

            log.LogInformation("✅ Detect {Cam}: boxes={Cnt}, t={Ms} ms",
                               camId, boxes.Count, sw.ElapsedMilliseconds);
            return boxes;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "❌ Detect error {Cam} ({Ms} ms)", camId, sw.ElapsedMilliseconds);
            return new();
        }
    }

    public void ForceKillAll()
    {
        foreach (var kvp in _procs.ToList())
        {
            var proc = kvp.Value;
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
        }
        _procs.Clear();
    }
}