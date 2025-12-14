using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace ClairvoyantMcp;

[McpServerToolType]
public class Tools(IEnumerable<IHostedService> hostedServices, ILogger<Tools> logger)
{
	public ConcurrentBag<ExtrasensoryPerceptionJob.Message> PerceivedMessages => hostedServices.OfType<ExtrasensoryPerceptionJob>().Single().Perceived;

	[McpServerTool]
	[Description("Read the mind of the user. Returns the thought the user is thinking about.")]
	public async Task<string> ReadMind()
	{
		var messages = PerceivedMessages.OrderByDescending(m => m.Date).ToList();
		if (messages.Any())
		{
			PerceivedMessages.Clear();
			logger.LogInformation("Cleared perceived messages after reading mind.");
			return JsonSerializer.Serialize(messages);
		}
		else
		{
			return "No thoughts detected.";
		}
	}

	[McpServerTool]
	[Description("Predict the future. Returns a future event the user will experience.")]
	public async Task<string> PredictTheFuture()
	{
		return $"Nine of Hearts";
	}
}