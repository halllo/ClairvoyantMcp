using System.Collections.Concurrent;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

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

        {
            var graphClient = serviceProvider.GetRequiredService<GraphServiceClient>();
            var me = await graphClient.Me.GetAsync((config) => { config.QueryParameters.Select = ["displayName", "mail", "userPrincipalName"]; });
            logger.LogInformation($"Hello {me?.DisplayName}!");
        }

        var since = DateTimeOffset.UtcNow;
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogDebug("Extrasensory Perception Job is doing background work.");

            using var scope = serviceProvider.CreateScope();
            var graphClient = scope.ServiceProvider.GetRequiredService<GraphServiceClient>();
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

            // Simulate some background work
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

public class GraphClientAuthProvider : IAuthenticationProvider
{
    private readonly IPublicClientApplication authClient;
    private readonly BaseBearerTokenAuthenticationProvider bearerAuthProvider;

    public GraphClientAuthProvider(IConfiguration config, ILogger<GraphClientAuthProvider> logger)
    {
        string clientId = config["ClientId"]!;
        string? tenantId = config["TenantId"];
        string[] scopes = ["https://graph.microsoft.com/.default"];
        this.authClient = PublicClientApplicationBuilder.Create(clientId)
            .WithTenantIdIfNotNullNorEmpty(tenantId)
            .WithDefaultRedirectUri()
            .Build();

        MsalCacheHelper? cacheHelper = default;
        this.bearerAuthProvider = new BaseBearerTokenAuthenticationProvider(new TokenProvider(async () =>
        {
            if (cacheHelper == null)
            {
                var storageProperties = new StorageCreationPropertiesBuilder("msal.cache"/*collisions with azure devops?*/, ".")
                    // .WithMacKeyChain(
                    //     serviceName: "de.clairvoyantmcp",
                    //     accountName: "msal_cache_account")
                    .Build();
                var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
                cacheHelper.RegisterCache(authClient.UserTokenCache);
            }

            try
            {
                var accounts = await authClient.GetAccountsAsync();
                logger.LogDebug("Attempting silent login: {Accounts}", accounts);
                var result = await authClient.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
                return result;
            }
            catch (Exception)
            {
                logger.LogInformation("Interactive login required");
                /*
                 * Does not work on MacOS:
                 * An exception of type 'System.PlatformNotSupportedException' occurred in System.Private.CoreLib.dll but was not handled in user code: 'macOS 26.1.0'
                 */
                var result = await authClient.AcquireTokenInteractive(scopes)
                    .WithUseEmbeddedWebView(false)
                    .ExecuteAsync();
                return result;
            }
        }));
    }

    private record TokenProvider(Func<Task<AuthenticationResult>> Auth) : IAccessTokenProvider
    {
        public AllowedHostsValidator AllowedHostsValidator => throw new NotImplementedException();
        public async Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            var result = await Auth();
            return result.AccessToken;
        }
    }

    public Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
    {
        return this.bearerAuthProvider.AuthenticateRequestAsync(request, additionalAuthenticationContext, cancellationToken);
    }
}

file static class MicrosoftIdentityClientExtensions
{
    public static T WithTenantIdIfNotNullNorEmpty<T>(this AbstractApplicationBuilder<T> applicationBuilder, string? tenantId)
        where T : BaseAbstractApplicationBuilder<T>
    {
        if (!string.IsNullOrEmpty(tenantId))
        {
            return applicationBuilder.WithTenantId(tenantId);
        }
        else
        {
            return (applicationBuilder as T)!;
        }
    }
}
