using SheetsMcp.Models;

namespace SheetsMcp.Preview;

public sealed record BatchPreviewOperation(
    string OperationId,
    string SpreadsheetId,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<BatchValueUpdateInput> Updates);
