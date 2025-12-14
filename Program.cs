using System.Security.Claims;
using ClairvoyantMcp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Graph;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<FileBasedTokenStore>();
builder.Services.AddSingleton<GraphClientAuth>();
builder.Services.AddScoped(sp =>
{
    var accountId = sp.GetRequiredService<FileBasedTokenStore>().GetAccountIds().FirstOrDefault() ?? "";// In production, you would get the account ID from the logged in user context.
    var auth = sp.GetRequiredService<GraphClientAuth>();
    return new GraphServiceClient(auth.GetAuthenticationProvider(accountId));
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
        o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        o.Scope.Add("openid");
        o.Scope.Add("profile");
        o.Scope.Add("offline_access");
        o.Scope.Add("https://graph.microsoft.com/.default");

        o.Events = new OpenIdConnectEvents
        {
            OnAuthorizationCodeReceived = async ctx =>
            {
                var auth = ctx.HttpContext.RequestServices.GetRequiredService<GraphClientAuth>();
                var account = await auth.AcquireTokenByAuthorizationCode(ctx);

                ctx.HandleCodeRedemption();

                ctx.Principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, account.Identifier)], ctx.Scheme.Name));

                ctx.Success();
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
