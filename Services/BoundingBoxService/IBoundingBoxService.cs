using stream_multi_cam.Models;

namespace stream_multi_cam.Services.BoundingBoxService
{
    public interface IBoundingBoxService
    {
        void UpdateBoxes(string cameraId, int sn, List<List<int>> rawBoxes);
    }
}
