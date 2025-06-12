namespace stream_multi_cam.Models
{
    public class BoundingBoxPayload
    {
        public string CameraId { get; set; }

        // Danh sách các bounding box (x,y,width,height,label)
        public List<List<int>> Boxes { get; set; }
    }
}
