using Microsoft.Extensions.Options;
using Worker.Config;
using Worker.Services;

namespace Worker;

public class ServiceWorker : BackgroundService
{
    private readonly ILogger<ServiceWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppSettings _settings;

    public ServiceWorker(
        ILogger<ServiceWorker> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<AppSettings> settings)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Worker start");

                using var scope = _scopeFactory.CreateScope();
                var processService = scope.ServiceProvider.GetRequiredService<ProcessService>();
                await processService.Process();

                await Task.Delay(TimeSpan.FromMinutes(_settings.IntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error in worker loop");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
