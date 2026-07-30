
namespace Worker.Config
{
    // Runtime descriptor for one PocketBase instance (built in Program.cs, not bound to config).
    public class PocketbaseInstance
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class AppSettings
    {
        // Shared credentials — same login on both instances.
        public string PocketbaseUser { get; set; } = "";
        public string PocketbasePassword { get; set; } = "";

        // One instance per side; only the URL differs.
        public string ApPocketbaseUrl { get; set; } = "";
        public string ArPocketbaseUrl { get; set; } = "";

        public string ApEndpoint { get; set; } = "";
        public string ArEndpoint { get; set; } = "";
        public string ArPriceListEndpoint { get; set; } = "";
        public int IntervalMinutes { get; set; } = 10;
    }
}
