namespace SheetsMcp.Configuration;

public sealed record SheetsMcpOptions(
    string OAuthClientConfigPath,
    string OAuthTokenStorePath,
    string? AuditLogPath)
{
    public const string AuditLogPathEnvVar = "SHEETSMCP_AUDIT_LOG_PATH";
    public const string DefaultOAuthUserId = "default";

    public static SheetsMcpOptions FromEnvironment()
    {
        var oauthClientConfigPath = Path.Combine(GetConfigDirectory(), "oauth_client.json");
        var oauthTokenStorePath = Path.Combine(GetDataDirectory(), "google-oauth");
        var auditLogPath = Environment.GetEnvironmentVariable(AuditLogPathEnvVar);

        return new SheetsMcpOptions(
            oauthClientConfigPath,
            oauthTokenStorePath,
            string.IsNullOrWhiteSpace(auditLogPath) ? null : ExpandHome(auditLogPath.Trim()));
    }

    public void ValidateOAuthClientConfigExists()
    {
        if (!File.Exists(OAuthClientConfigPath))
        {
            throw new InvalidOperationException(
                $"Google OAuth desktop client config was not found at '{OAuthClientConfigPath}'. " +
                "Place it at the default per-user config path.");
        }
    }

    internal static string ExpandHome(string path)
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

    private static string GetConfigDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SheetsMCP");
        }

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "SheetsMCP");
        }

        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        return Path.Combine(
            string.IsNullOrWhiteSpace(xdgConfigHome)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
                : ExpandHome(xdgConfigHome.Trim()),
            "sheetsmcp");
    }

    private static string GetDataDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SheetsMCP");
        }

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "SheetsMCP");
        }

        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return Path.Combine(
            string.IsNullOrWhiteSpace(xdgDataHome)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
                : ExpandHome(xdgDataHome.Trim()),
            "sheetsmcp");
    }
}
