using System.Collections.Concurrent;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace ClairvoyantMcp;

public class ExtrasensoryPerceptionJob : BackgroundService
{
    private readonly IServiceProvider serviceProvider;

    private readonly ILogger<ExtrasensoryPerceptionJob> logger;

    public ConcurrentBag<Message> Perceived { get; private set; } = new();

    public ExtrasensoryPerceptionJob(IServiceProvider serviceProvider, ILogger<ExtrasensoryPerceptionJob> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Extrasensory Perception Job is starting.");

        var greeted = false;
        var since = DateTimeOffset.UtcNow;
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogDebug("Extrasensory Perception Job is doing background work.");
            try
            {
                using var scope = serviceProvider.CreateScope();
                var graphClient = scope.ServiceProvider.GetRequiredService<GraphServiceClient>();

                if (greeted == false)
                {
                    var me = await graphClient.Me.GetAsync((config) => { config.QueryParameters.Select = ["displayName", "mail", "userPrincipalName"]; });
                    logger.LogInformation($"Hello {me?.DisplayName}!");
                    greeted = true;
                }

                var messages = await GetMessages(graphClient, since);
                if (messages.Any())
                {
                    foreach (var msg in messages)
                    {
                        this.logger.LogInformation("New message perceived: {@Message}", msg);
                        Perceived.Add(msg);
                    }
                    since = messages.Max(m => m.Date)!.Value.AddSeconds(1);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while perceiving messages.");
            }
            
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }

        logger.LogInformation("Extrasensory Perception Job is stopping.");
    }

    private async Task<Message[]> GetMessages(GraphServiceClient graphClient, DateTimeOffset since)
    {
        //https://stackoverflow.com/a/73937365/6466378
        var mySelfMessagesResponse = await graphClient.Me.Chats["48:notes"].Messages.GetAsync();
        var mySelfMessages = mySelfMessagesResponse?.Value?
            .Select(m => new Message
            (
                Id: m.Id,
                Date: m.CreatedDateTime?.ToLocalTime(),
                Body: m.Body?.Content
            ))
            .Where(m => m.Date >= since)
            .ToArray() ?? [];

        return mySelfMessages;
    }

    public record Message(string? Id, DateTimeOffset? Date, string? Body);
}
