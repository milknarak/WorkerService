
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
        // Per-instance credentials — each PocketBase instance has its own superuser
        // (identity = <instance-id>@ztrus.io), so AP and AR do NOT share a login.
        public string ApPocketbaseUser { get; set; } = "";
        public string ApPocketbasePassword { get; set; } = "";
        public string ArPocketbaseUser { get; set; } = "";
        public string ArPocketbasePassword { get; set; } = "";

        // One instance per side.
        public string ApPocketbaseUrl { get; set; } = "";
        public string ArPocketbaseUrl { get; set; } = "";

        public string ApEndpoint { get; set; } = "";
        public string ArEndpoint { get; set; } = "";
        public string ArPriceListEndpoint { get; set; } = "";
        public int IntervalMinutes { get; set; } = 10;
    }
}
