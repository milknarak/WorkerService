namespace Worker.Models
{
    public class SapResponse
    {
        public ResponseApi? responseApi { get; set; }
        public ResponseRefData? responseRefData { get; set; }
    }

    public class ResponseApi
    {
        public string? statusCode { get; set; }
        public string? statusDesc { get; set; }
    }

    public class ResponseRefData
    {
        public int? processId { get; set; }
        public string? processStatus { get; set; }
        public string? processErrMsg { get; set; }
        public string? tranNo { get; set; }
    }
}
