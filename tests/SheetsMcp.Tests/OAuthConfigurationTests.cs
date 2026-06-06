using SheetsMcp.Configuration;
using SheetsMcp.Google;

namespace SheetsMcp.Tests;

public sealed class OAuthConfigurationTests
{
    [Fact]
    public void FromEnvironment_uses_default_oauth_paths_and_audit_override()
    {
        var auditLog = Path.Combine(Path.GetTempPath(), $"sheetsmcp-audit-{Guid.NewGuid():N}.log");

        WithEnvironment(SheetsMcpOptions.AuditLogPathEnvVar, auditLog, () =>
        {
            var options = SheetsMcpOptions.FromEnvironment();

            Assert.EndsWith(Path.Combine("sheetsmcp", "oauth_client.json"), options.OAuthClientConfigPath, StringComparison.Ordinal);
            Assert.EndsWith(Path.Combine("sheetsmcp", "google-oauth"), options.OAuthTokenStorePath, StringComparison.Ordinal);
            Assert.Equal(auditLog, options.AuditLogPath);
        });
    }

    [Fact]
    public async Task OAuth_status_reports_missing_client_config_without_token_values()
    {
        var options = new SheetsMcpOptions(
            Path.Combine(Path.GetTempPath(), $"missing-client-{Guid.NewGuid():N}.json"),
            Path.Combine(Path.GetTempPath(), $"missing-token-{Guid.NewGuid():N}"),
            null);
        var provider = new GoogleOAuthCredentialProvider(options);

        var status = await provider.GetStatusAsync(CancellationToken.None);

        Assert.False(status.ClientConfigExists);
        Assert.False(status.TokenAvailable);
        Assert.Contains("not found", status.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static void WithEnvironment(string name, string value, Action action)
    {
        var previous = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
        try
        {
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, previous);
        }
    }
}
