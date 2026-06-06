using System.Text.Json;
using SheetsMcp.Configuration;

namespace SheetsMcp.Services;

public sealed record AuditLogEntry(
    DateTimeOffset Timestamp,
    string Tool,
    string SpreadsheetId,
    string Target,
    string WriteType,
    int RowCount,
    int ColumnCount,
    int CellCount,
    bool Success);

public interface IAuditLogger
{
    Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken);
}

public sealed class FileAuditLogger(SheetsMcpOptions options) : IAuditLogger
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.AuditLogPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(options.AuditLogPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var line = JsonSerializer.Serialize(entry, SerializerOptions) + Environment.NewLine;
        await File.AppendAllTextAsync(options.AuditLogPath, line, cancellationToken);
    }
}
