using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using Worker;
using Worker.Config;
using Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));

builder.Services.AddHttpClient<PocketbaseService>()
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

builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<SapService>();
builder.Services.AddScoped<ProcessService>();

//builder.Services.AddHostedService<ServiceWorker>();

var host = builder.Build();

var settings = host.Services.GetRequiredService<IOptions<AppSettings>>().Value;

if (settings.DebugRunOnce)
{
    using var scope = host.Services.CreateScope();
    var process = scope.ServiceProvider.GetRequiredService<ProcessService>();

    await process.Process();
}
else
{
    host.Run();
}