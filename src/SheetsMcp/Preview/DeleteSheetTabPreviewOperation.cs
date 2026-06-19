namespace SheetsMcp.Preview;

public sealed record DeleteSheetTabPreviewOperation(
    string OperationId,
    string SpreadsheetId,
    DateTimeOffset ExpiresAt,
    int SheetId,
    string Title);
