namespace SheetsMcp.Configuration;

public enum WriteGuardrailMode
{
    PreviewRequired,
    Direct
}

public sealed record SheetsMcpOptions(
    string CredentialsPath,
    WriteGuardrailMode WriteGuardrails,
    string? AuditLogPath)
{
    public const string CredentialsEnvVar = "SHEETSMCP_GOOGLE_APPLICATION_CREDENTIALS";
    public const string GuardrailsEnvVar = "SHEETSMCP_WRITE_GUARDRAILS";
    public const string AuditLogPathEnvVar = "SHEETSMCP_AUDIT_LOG_PATH";

    public static SheetsMcpOptions FromEnvironment()
    {
        var credentialsPath = Environment.GetEnvironmentVariable(CredentialsEnvVar);
        if (string.IsNullOrWhiteSpace(credentialsPath))
        {
            throw new InvalidOperationException($"{CredentialsEnvVar} must point to a Google service-account JSON key file.");
        }

        credentialsPath = ExpandHome(credentialsPath.Trim());
        if (!File.Exists(credentialsPath))
        {
            throw new InvalidOperationException($"{CredentialsEnvVar} points to a file that does not exist.");
        }

        var guardrails = ParseGuardrails(Environment.GetEnvironmentVariable(GuardrailsEnvVar));
        var auditLogPath = Environment.GetEnvironmentVariable(AuditLogPathEnvVar);

        return new SheetsMcpOptions(credentialsPath, guardrails, string.IsNullOrWhiteSpace(auditLogPath) ? null : ExpandHome(auditLogPath.Trim()));
    }

    private static WriteGuardrailMode ParseGuardrails(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("preview-required", StringComparison.OrdinalIgnoreCase))
        {
            return WriteGuardrailMode.PreviewRequired;
        }

        if (value.Equals("direct", StringComparison.OrdinalIgnoreCase))
        {
            return WriteGuardrailMode.Direct;
        }

        throw new InvalidOperationException($"{GuardrailsEnvVar} must be 'preview-required' or 'direct'.");
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
}
