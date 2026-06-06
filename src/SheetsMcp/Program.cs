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
    Console.WriteLine("Run 'sheetsmcp auth login' before using Sheets tools.");
    Console.WriteLine("OAuth client config: default per-user config path.");
    return;
}

if (args.Length > 0 && args[0].Equals("auth", StringComparison.OrdinalIgnoreCase))
{
    await RunAuthCommandAsync(args[1..]);
    return;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton(_ => SheetsMcpOptions.FromEnvironment());
builder.Services.AddSingleton<GoogleOAuthCredentialProvider>();
builder.Services.AddSingleton<ISheetsServiceFactory, GoogleSheetsServiceFactory>();
builder.Services.AddSingleton<ISheetsService, GoogleSheetsService>();
builder.Services.AddSingleton<IBatchPreviewStore, InMemoryBatchPreviewStore>();
builder.Services.AddSingleton<IFormattingPreviewStore, InMemoryFormattingPreviewStore>();
builder.Services.AddSingleton<IAuditLogger, FileAuditLogger>();
builder.Services.AddSingleton<ISpreadsheetToolService, SpreadsheetToolService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

static async Task RunAuthCommandAsync(string[] args)
{
    if (args.Length == 0 || args[0].Equals("--help", StringComparison.OrdinalIgnoreCase) || args[0].Equals("-h", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Usage: sheetsmcp auth <login|status|logout> [--yes]");
        return;
    }

    var options = SheetsMcpOptions.FromEnvironment();
    var provider = new GoogleOAuthCredentialProvider(options);
    var command = args[0];

    if (command.Equals("login", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Using OAuth client config: {options.OAuthClientConfigPath}");
        Console.WriteLine($"Using OAuth token store: {options.OAuthTokenStorePath}");
        await provider.AuthorizeAsync(CancellationToken.None);
        Console.WriteLine("Google OAuth login completed.");
        return;
    }

    if (command.Equals("status", StringComparison.OrdinalIgnoreCase))
    {
        var status = await provider.GetStatusAsync(CancellationToken.None);
        Console.WriteLine($"OAuth client config: {(status.ClientConfigExists ? "found" : "missing")} ({options.OAuthClientConfigPath})");
        Console.WriteLine($"OAuth token cache: {(status.TokenAvailable ? "available" : "missing")} ({options.OAuthTokenStorePath})");
        Console.WriteLine(status.Message);
        return;
    }

    if (command.Equals("logout", StringComparison.OrdinalIgnoreCase))
    {
        if (!args.Contains("--yes", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine("Refusing to delete the OAuth token store without --yes.");
            return;
        }

        await provider.LogoutAsync();
        Console.WriteLine("Google OAuth token cache removed.");
        return;
    }

    Console.WriteLine("Unknown auth command. Usage: sheetsmcp auth <login|status|logout> [--yes]");
}
