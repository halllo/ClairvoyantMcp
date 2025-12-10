var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapMcp("/mcp");

app.Run();
