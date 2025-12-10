using System.ComponentModel;
using ModelContextProtocol.Server;

namespace ClairvoyantMcp;

[McpServerToolType]
public class Tools(IEnumerable<IHostedService> hostedServices)
{
	[McpServerTool]
	[Description("Read the mind of the user. Returns the thought the user is thinking about.")]
	public async Task<string> ReadMind()
	{
		return $"King of Hearts";
	}

	[McpServerTool]
	[Description("Predict the future. Returns a future event the user will experience.")]
	public async Task<string> PredictTheFuture()
	{
		return $"Five of Spades";
	}

	[McpServerTool]
	[Description("Get the counter.")]
	public async Task<int> GetCounter()
	{
		var espJob = hostedServices.OfType<ExtrasensoryPerceptionJob>().Single();
		return espJob.Counter;
	}
}