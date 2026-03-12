using Worker.Services;
using Worker.Mappers;

namespace Worker;

public class ServiceWorker : BackgroundService
{
    private readonly ILogger<ServiceWorker> _logger;
    private readonly ProcessService _processService;

    public ServiceWorker(
        ILogger<ServiceWorker> logger,
        ProcessService processService)
    {
        _logger = logger;
        _processService = processService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker start");

            await _processService.Process();

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
