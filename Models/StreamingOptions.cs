namespace stream_multi_cam.Models
{
    public class StreamingOptions
    {
        public string UrlModel { get; init; } = "";
        public string FfmpegPath { get; init; } = "";
        public bool EnableLLHls { get; init; }
        public int HlsTime { get; init; } = 1;
        public int HlsListSize { get; init; } = 3;
        public bool CopyCodec { get; init; } = true;
        public bool DebugFfmpeg { get; init; }
    }
}
