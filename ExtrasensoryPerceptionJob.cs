public class ExtrasensoryPerceptionJob : BackgroundService
{
    private readonly ILogger<ExtrasensoryPerceptionJob> _logger;

    public int Counter { get; private set; } = 0;

    public ExtrasensoryPerceptionJob(ILogger<ExtrasensoryPerceptionJob> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Extrasensory Perception Job is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Extrasensory Perception Job is doing background work.");
            Counter++;

            // Simulate some background work
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }

        _logger.LogInformation("Extrasensory Perception Job is stopping.");
    }
}