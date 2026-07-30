using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using Worker;
using Worker.Config;
using Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));

var appSettings = builder.Configuration.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings();

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHttpClient("pocketbase")
.ConfigurePrimaryHttpMessageHandler(() =>
{
    return new SocketsHttpHandler
    {
        ConnectCallback = async (context, token) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host);

            var ipv4 = addresses.First(a =>
                a.AddressFamily == AddressFamily.InterNetwork);

            var socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);

            await socket.ConnectAsync(ipv4, context.DnsEndPoint.Port);

            return new NetworkStream(socket, ownsSocket: true);
        }
    };
});

// AP and AR are separate PocketBase instances, each with its own superuser login and URL.
// One TransactionService (backed by its own PocketbaseService) per side; ProcessService injects
// IEnumerable<TransactionService> and runs the pipeline for each.
var instances = new[]
{
    new PocketbaseInstance
    {
        Name = "AP",
        Url = appSettings.ApPocketbaseUrl,
        User = appSettings.ApPocketbaseUser,
        Password = appSettings.ApPocketbasePassword,
    },
    new PocketbaseInstance
    {
        Name = "AR",
        Url = appSettings.ArPocketbaseUrl,
        User = appSettings.ArPocketbaseUser,
        Password = appSettings.ArPocketbasePassword,
    },
};

foreach (var instance in instances)
{
    // URL ว่าง/ผิด → skip เฉพาะ instance นั้น (ไม่ให้ new Uri ใน ctor พังลากทั้งระบบ เช่น AR พังทำ AP ล่มด้วย)
    if (!Uri.TryCreate(instance.Url, UriKind.Absolute, out _))
    {
        Console.WriteLine($"[startup] Skipping PocketBase instance '{instance.Name}' — invalid or empty Url: '{instance.Url}'");
        continue;
    }

    var pb = instance;
    builder.Services.AddScoped(sp =>
    {
        var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("pocketbase");
        var pocketbase = new PocketbaseService(
            http,
            pb,
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<PocketbaseService>>());
        return new TransactionService(pocketbase);
    });
}

builder.Services.AddHttpClient<SapService>();
builder.Services.AddScoped<ProcessService>();

builder.Services.AddHostedService<ServiceWorker>();

var host = builder.Build();
host.Run();