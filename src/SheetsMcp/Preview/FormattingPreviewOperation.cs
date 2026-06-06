using SheetsMcp.Models;

namespace SheetsMcp.Preview;

public sealed record FormattingPreviewOperation(
    string OperationId,
    string SpreadsheetId,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<FormattingUpdateInput> Updates,
    IReadOnlyList<string> Fields);
