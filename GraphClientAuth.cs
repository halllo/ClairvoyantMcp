using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;

public class GraphClientAuth(IConfiguration config, FileBasedTokenStore tokenStore, ILogger<GraphClientAuth> logger)
{
    private readonly string[] scopes = ["https://graph.microsoft.com/.default"];

    public async Task<AccountId> AcquireTokenByAuthorizationCode(AuthorizationCodeReceivedContext ctx)
    {
        var cca = ConfidentialClientApplicationBuilder
            .Create(config["ClientId"])
            .WithClientSecret(config["ClientSecret"])
            .WithAuthority($"{config["Instance"]}{config["TenantId"]}/v2.0")
            .WithRedirectUri($"{ctx.HttpContext.Request.Scheme}://{ctx.HttpContext.Request.Host}{config["CallbackPath"]}")
            .Build();

        cca.UserTokenCache.SetAfterAccessAsync(async args =>
        {
            if (args.HasStateChanged)
            {
                var tokens = args.TokenCache.SerializeMsalV3();
                await tokenStore.StoreAsync(args.Account.HomeAccountId.Identifier, tokens);
            }
        });

        var token = await cca.AcquireTokenByAuthorizationCode(scopes, ctx.ProtocolMessage.Code)
            .WithPkceCodeVerifier(ctx.TokenEndpointRequest!.Parameters["code_verifier"])
            .ExecuteAsync();

        var account = token.Account.HomeAccountId;

        logger.LogInformation("User {AccountId} connected his Graph API.", account.Identifier);

        return account;
    }

    public IAuthenticationProvider GetAuthenticationProvider(string accountId)
    {
        if (string.IsNullOrEmpty(accountId))
        {
            throw new ArgumentException("AccountId must be provided to get an authentication provider.", nameof(accountId));
        }

        var cca = ConfidentialClientApplicationBuilder
            .Create(config["ClientId"]!)
            .WithClientSecret(config["ClientSecret"]!)
            .WithAuthority($"{config["Instance"]}{config["TenantId"]}/v2.0")
            .Build();

        cca.UserTokenCache.SetBeforeAccessAsync(async args =>
        {
            var tokens = await tokenStore.LoadAsync(accountId);
            args.TokenCache.DeserializeMsalV3(tokens);
        });

        cca.UserTokenCache.SetAfterAccessAsync(async args =>
        {
            if (args.HasStateChanged)
            {
                var tokens = args.TokenCache.SerializeMsalV3();
                await tokenStore.StoreAsync(accountId, tokens);
            }
        });

        var bearerAuthProvider = new BaseBearerTokenAuthenticationProvider(new TokenProvider(async () =>
        {
            try
            {
                var account = await cca.GetAccountAsync(accountId);
                logger.LogDebug("Attempting silent login: {@Account}", account);
                var result = await cca.AcquireTokenSilent(scopes, account).ExecuteAsync();
                return result;
            }
            catch (Exception)
            {
                logger.LogError("Interactive login required");
                throw;
            }
        }));

        return bearerAuthProvider;
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
}
