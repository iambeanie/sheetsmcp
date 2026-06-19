namespace SheetsMcp.Models;

public sealed record SheetSummary(
    string Title,
    int? SheetId,
    int RowCount,
    int ColumnCount,
    bool Hidden,
    string SheetType);

public sealed record SpreadsheetMetadataResult(
    string SpreadsheetId,
    string? Title,
    IReadOnlyList<SheetSummary> Sheets);

public sealed record ReadRangeResult(
    string SpreadsheetId,
    string Range,
    int RowCount,
    int ColumnCount,
    IReadOnlyList<IReadOnlyList<object?>> Values);

public sealed record FindValuesResult(
    string SpreadsheetId,
    string Query,
    int MatchCount,
    IReadOnlyList<FindValueMatch> Matches);

public sealed record FindValueMatch(
    string Range,
    string Text);

public sealed record WriteResult(
    string SpreadsheetId,
    string Tool,
    string Range,
    int UpdatedRows,
    int UpdatedColumns,
    int UpdatedCells);

public sealed record AppendResult(
    string SpreadsheetId,
    string Range,
    string? UpdatedRange,
    int UpdatedRows,
    int UpdatedColumns,
    int UpdatedCells);

public sealed record BatchValueUpdateInput(
    string Range,
    IReadOnlyList<IReadOnlyList<object?>> Values);

public sealed record BatchPreviewResult(
    string OperationId,
    string SpreadsheetId,
    DateTimeOffset ExpiresAt,
    int RangeCount,
    int TotalRows,
    int TotalColumns,
    int TotalCells,
    IReadOnlyList<BatchPreviewRange> Ranges);

public sealed record BatchPreviewRange(
    string Range,
    int RowCount,
    int ColumnCount,
    int CellCount);

public sealed record BatchConfirmResult(
    string OperationId,
    string SpreadsheetId,
    int UpdatedRows,
    int UpdatedColumns,
    int UpdatedCells,
    IReadOnlyList<string> UpdatedRanges);

public sealed record SheetTabResult(
    string SpreadsheetId,
    int SheetId,
    string Title,
    string Action);

public sealed record DeleteSheetTabPreviewResult(
    string OperationId,
    string SpreadsheetId,
    DateTimeOffset ExpiresAt,
    int SheetId,
    string Title);

public sealed record DeleteSheetTabConfirmResult(
    string OperationId,
    string SpreadsheetId,
    int SheetId,
    string Title,
    string Action);
