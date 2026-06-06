using System.Text.Json;
using SheetsMcp.Configuration;
using SheetsMcp.Services;

namespace SheetsMcp.Tests;

public sealed class AuditLoggerTests
{
    [Fact]
    public async Task FileAuditLogger_writes_metadata_without_cell_values()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sheetsmcp-audit-{Guid.NewGuid():N}.log");
        var logger = new FileAuditLogger(new SheetsMcpOptions("/tmp/credentials.json", WriteGuardrailMode.PreviewRequired, path));

        await logger.WriteAsync(new AuditLogEntry(
            DateTimeOffset.Parse("2026-04-26T00:00:00Z"),
            "update_range",
            "spreadsheet",
            "Sheet1!A1:B2",
            "update",
            2,
            2,
            4,
            true), CancellationToken.None);

        var text = await File.ReadAllTextAsync(path);
        using var document = JsonDocument.Parse(text);

        Assert.Equal("update_range", document.RootElement.GetProperty("tool").GetString());
        Assert.Equal(4, document.RootElement.GetProperty("cellCount").GetInt32());
        Assert.DoesNotContain("secret cell value", text, StringComparison.OrdinalIgnoreCase);
    }
}
