using System.Threading.Tasks;

namespace stream_multi_cam.Services.CameraStreamService
{
    public interface ICameraStreamService
    {
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }
}
