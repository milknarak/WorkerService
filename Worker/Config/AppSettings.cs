
namespace Worker.Config
{
    public class AppSettings
    {
        public bool DebugRunOnce { get; set; }
        public string PocketbaseUrl { get; set; }
        public string PocketbaseUser { get; set; }
        public string PocketbasePassword { get; set; }
        public string SapApiUrl { get; set; }
        public int IntervalMinutes { get; set; }
    }
}
