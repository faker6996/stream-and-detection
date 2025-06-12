using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using stream_multi_cam.Hubs;
using stream_multi_cam.Models;
using stream_multi_cam.Services.BoundingBoxService;
using stream_multi_cam.Services.CameraStreamService;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Đọc config cameras từ appsettings.json
builder.Services.Configure<List<CameraConfig>>(builder.Configuration.GetSection("Cameras"));
builder.Services.Configure<IPCameraRoot>(builder.Configuration.GetSection("IPCameraRoot"));

builder.Services.Configure<StreamingOptions>(
    builder.Configuration.GetSection("Streaming"));

builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<List<CameraConfig>>>().Value);

// Đăng ký Services & Hubs
builder.Services.AddSingleton<ICameraStreamService, CameraStreamService>();
builder.Services.AddSingleton<IBoundingBoxService, BoundingBoxService>();
builder.Services.AddHostedService<CameraStreamService>();
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(options =>
{
    options.DetailedErrors = true;
});

// Serilog cấu hình
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)      // lấy mức LogLevel từ appsettings
    .WriteTo.Console()
    .WriteTo.File("Logs/app-.log",
                  rollingInterval: RollingInterval.Day,
                  retainedFileCountLimit: 7)             // giữ 7 file gần nhất
    .CreateLogger();
builder.Host.UseSerilog();   // quan trọng!

var app = builder.Build();

// Cấu hình Content Type Provider cho HLS files
var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".m3u8"] = "application/vnd.apple.mpegurl";
provider.Mappings[".ts"] = "video/mp2t";

// Static files cho wwwroot (CSS, JS, images, etc.)
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

// QUAN TRỌNG: Static files riêng cho HLS streaming
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.WebRootPath, "hls")),
    RequestPath = "/hls", // URL path sẽ là /hls/...
    ContentTypeProvider = provider,
    ServeUnknownFileTypes = true, // Cho phép serve các file type không được định nghĩa
    DefaultContentType = "application/octet-stream",
    OnPrepareResponse = context =>
    {
        // Thêm CORS headers cho HLS files
        context.Context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        context.Context.Response.Headers.Add("Access-Control-Allow-Methods", "GET");
        context.Context.Response.Headers.Add("Access-Control-Allow-Headers", "Range");

        // Cache control cho live streaming
        if (context.File.Name.EndsWith(".m3u8"))
        {
            // Playlist files - no cache
            context.Context.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            context.Context.Response.Headers.Add("Pragma", "no-cache");
            context.Context.Response.Headers.Add("Expires", "0");
        }
        else if (context.File.Name.EndsWith(".ts"))
        {
            // Segment files - cache for short time
            context.Context.Response.Headers.Add("Cache-Control", "public, max-age=10");
        }
    }
});

// Logging middleware để debug requests
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/hls"))
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("HLS Request: {Method} {Path}", context.Request.Method, context.Request.Path);

        await next();

        logger.LogInformation("HLS Response: {StatusCode} for {Path}", context.Response.StatusCode, context.Request.Path);
    }
    else
    {
        await next();
    }
});

app.MapControllers();
app.MapBlazorHub();
app.MapHub<OverlayHub>("/overlayHub");
app.MapFallbackToPage("/_Host");

// Debug: Log HLS directory path at startup
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var hlsPath = Path.Combine(app.Environment.WebRootPath, "hls");
logger.LogInformation("HLS files will be served from: {HlsPath}", hlsPath);
logger.LogInformation("HLS URL pattern: /hls/{{cameraId}}/index.m3u8");

// Ensure HLS directory exists at startup
Directory.CreateDirectory(hlsPath);
logger.LogInformation("HLS directory created/verified: {HlsPath}", hlsPath);


//// AppDomain exit hook
//AppDomain.CurrentDomain.ProcessExit += (s, e) =>
//{
//    var streamService = app.Services.GetService<ICameraStreamService>() as CameraStreamService;

//    if (streamService is not null)
//    {
//        Console.WriteLine("⚠️ App exiting – killing ffmpeg processes");
//        streamService.ForceKillAll();
//    }
//};


app.Run();