namespace stream_multi_cam.Models
{
    public class CameraConfig
    {
        public string CameraId { get; set; }
        public int? Channel { get; set; }
        public int? Fps { get; set; }
        public int? subtype { get; set; }
        public string Location { get; set; }
        public int SubstreamWidth { get; set; }
        public int SubstreamHeight { get; set; }

    } 
    
    public class IPCameraRoot
    {
        public string Url { get; set; }
    }
}
