using SheetsMcp.Configuration;
using SheetsMcp.Google;
using SheetsMcp.Services;

namespace SheetsMcp.Tests;

[Trait("Category", "Integration")]
public sealed class GoogleIntegrationTests
{
    private const string SpreadsheetEnvVar = "SHEETSMCP_INTEGRATION_SPREADSHEET";
    private const string WriteEnvVar = "SHEETSMCP_INTEGRATION_WRITE";

    [Fact]
    [Trait("Type", "Read")]
    public async Task Metadata_can_be_read_when_integration_environment_is_configured()
    {
        var spreadsheet = Environment.GetEnvironmentVariable(SpreadsheetEnvVar);
        var credentials = Environment.GetEnvironmentVariable(SheetsMcpOptions.CredentialsEnvVar);

        if (string.IsNullOrWhiteSpace(spreadsheet) || string.IsNullOrWhiteSpace(credentials) || !File.Exists(ExpandHome(credentials)))
        {
            return;
        }

        var options = new SheetsMcpOptions(ExpandHome(credentials), WriteGuardrailMode.PreviewRequired, null);
        var service = new GoogleSheetsService(new GoogleSheetsServiceFactory(options));

        var result = await service.GetMetadataAsync(spreadsheet, CancellationToken.None);

        Assert.Equal(spreadsheet, result.SpreadsheetId);
        Assert.NotEmpty(result.Sheets);
    }

    [Fact]
    [Trait("Type", "Write")]
    public async Task AppendRows_can_write_when_integration_write_is_enabled()
    {
        var context = CreateIntegrationContext();
        if (context is null || !string.Equals(Environment.GetEnvironmentVariable(WriteEnvVar), "1", StringComparison.Ordinal))
        {
            return;
        }

        var metadata = await context.Service.GetMetadataAsync(context.SpreadsheetId, CancellationToken.None);
        var firstSheet = metadata.Sheets.First().Title;
        var result = await context.Service.AppendRowsAsync(
            context.SpreadsheetId,
            firstSheet,
            [["SheetsMCP integration validation", DateTimeOffset.UtcNow.ToString("O")]],
            CancellationToken.None);

        Assert.True(result.UpdatedRows >= 1);
        Assert.True(result.UpdatedCells >= 2);
    }

    private static IntegrationContext? CreateIntegrationContext()
    {
        var spreadsheet = Environment.GetEnvironmentVariable(SpreadsheetEnvVar);
        var credentials = Environment.GetEnvironmentVariable(SheetsMcpOptions.CredentialsEnvVar);

        if (string.IsNullOrWhiteSpace(spreadsheet) || string.IsNullOrWhiteSpace(credentials) || !File.Exists(ExpandHome(credentials)))
        {
            return null;
        }

        var options = new SheetsMcpOptions(ExpandHome(credentials), WriteGuardrailMode.PreviewRequired, null);
        return new IntegrationContext(spreadsheet, new GoogleSheetsService(new GoogleSheetsServiceFactory(options)));
    }

    private static string ExpandHome(string path)
    {
        if (path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (path.StartsWith("~/", StringComparison.Ordinal))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        }

        return path;
    }

    private sealed record IntegrationContext(string SpreadsheetId, ISheetsService Service);
}
