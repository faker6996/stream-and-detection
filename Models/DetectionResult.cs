namespace stream_multi_cam.Models
{

    public class DetectionResult
    {
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public List<ResultData> Data { get; set; }
    }

    public class ResultData
    {
        public int TotalPerson { get; set; }
        public List<List<int>> Boxes { get; set; }
    }
}
