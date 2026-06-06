using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SheetsMcp.Configuration;
using SheetsMcp.Google;
using SheetsMcp.Preview;
using SheetsMcp.Services;

if (args.Contains("--version", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("SheetsMCP 0.1.0");
    return;
}

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("SheetsMCP local MCP stdio server for Google Sheets.");
    Console.WriteLine("Required: SHEETSMCP_GOOGLE_APPLICATION_CREDENTIALS=/path/to/service-account.json");
    return;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton(_ => SheetsMcpOptions.FromEnvironment());
builder.Services.AddSingleton<ISheetsServiceFactory, GoogleSheetsServiceFactory>();
builder.Services.AddSingleton<ISheetsService, GoogleSheetsService>();
builder.Services.AddSingleton<IBatchPreviewStore, InMemoryBatchPreviewStore>();
builder.Services.AddSingleton<IAuditLogger, FileAuditLogger>();
builder.Services.AddSingleton<ISpreadsheetToolService, SpreadsheetToolService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
