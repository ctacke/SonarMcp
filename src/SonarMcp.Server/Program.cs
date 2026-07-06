using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SonarMcp.Server;

var builder = Host.CreateApplicationBuilder(args);

// stdio transport uses stdout for the JSON-RPC protocol — all logging must go to stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(_ => SonarQubeConfig.FromEnvironment());

builder.Services.AddHttpClient<SonarQubeClient>((sp, client) =>
{
    var config = sp.GetRequiredService<SonarQubeConfig>();
    client.BaseAddress = new Uri(config.Url);
    var basicAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{config.Token}:"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
