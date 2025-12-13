using System.Security.Claims;
using ClairvoyantMcp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<GraphClientAuthProvider>();
builder.Services.AddSingleton(sp =>
{
    var authProvider = sp.GetRequiredService<GraphClientAuthProvider>();
    return new GraphServiceClient(authProvider);
});
builder.Services.AddHostedService<ExtrasensoryPerceptionJob>();

builder.Services.AddMcpServer(o => o.ServerInfo = new()
{
    Name = "Clairvoyant MCP Server",
    Description = "Read minds and predict the future.",
    Version = "1.0.0",
})
    .WithHttpTransport(o => o.Stateless = true)
    .WithToolsFromAssembly(typeof(Program).Assembly)
    ;

builder.Services.AddAuthentication()
    .AddCookie()
    .AddOpenIdConnect("connect", o =>
    {
        o.Authority = $"{builder.Configuration["Instance"]}{builder.Configuration["TenantId"]}/v2.0";
        o.ClientId = builder.Configuration["ClientId"];
        o.ClientSecret = builder.Configuration["ClientSecret"];
        o.CallbackPath = builder.Configuration["CallbackPath"];
        o.ResponseType = OpenIdConnectResponseType.Code;
        o.SaveTokens = false; //we will store the tokens ourselves
        o.Scope.Add("openid");
        o.Scope.Add("profile");
        o.Scope.Add("offline_access");
        o.Scope.Add("https://graph.microsoft.com/.default");

        o.Events = new OpenIdConnectEvents
        {
            OnAuthorizationCodeReceived = async ctx =>
            {
                var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                
                var tid = ctx.Principal.FindFirstValue("tid");
                var oid = ctx.Principal.FindFirstValue("oid");
                var cacheKey = $"{oid}.{tid}";
                logger.LogInformation("Authorization code received for tenant {TenantId} and object {ObjectId}", tid, oid);

                var cca = ConfidentialClientApplicationBuilder
                    .Create(builder.Configuration["ClientId"])
                    .WithClientSecret(builder.Configuration["ClientSecret"])
                    .WithAuthority(new Uri(o.Authority))
                    .WithRedirectUri($"{ctx.HttpContext.Request.Scheme}://{ctx.HttpContext.Request.Host}{o.CallbackPath}")
                    .Build();

                var token = await cca.AcquireTokenByAuthorizationCode(["https://graph.microsoft.com/.default"], ctx.ProtocolMessage.Code).ExecuteAsync();
                ctx.HandleCodeRedemption();
                var homeAccountId = token.Account.HomeAccountId.Identifier;
                var appUserId = ctx.Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? $"{tid}.{oid}";

                logger.LogInformation("User {AppUserId} signed in with home account id {HomeAccountId}", appUserId, homeAccountId);
            }
        };
    })
    ;

builder.Services.AddAuthorization();



var app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");

app.MapGet("/connect", async (HttpContext ctx) =>
{
    await ctx.ChallengeAsync("connect", new AuthenticationProperties() { RedirectUri = "/" });
});

app.MapMcp("/mcp");

app.Run();
