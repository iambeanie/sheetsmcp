namespace SheetsMcp.Models;

public sealed record FormattingReadResult(
    string SpreadsheetId,
    string Range,
    int RowCount,
    int ColumnCount,
    IReadOnlyList<IReadOnlyList<CellFormattingResult>> Cells,
    IReadOnlyList<MergedRangeSummary> MergedRanges);

public sealed record CellFormattingResult(
    string Cell,
    CellFormatSnapshot Format,
    bool InMergedRange);

public sealed record CellFormatSnapshot(
    TextFormatSnapshot? TextFormat,
    string? TextColor,
    string? BackgroundColor,
    string? HorizontalAlignment,
    string? VerticalAlignment,
    string? WrapStrategy,
    NumberFormatSnapshot? NumberFormat,
    BordersSnapshot? Borders);

public sealed record TextFormatSnapshot(
    bool? Bold,
    bool? Italic,
    bool? Underline,
    bool? Strikethrough,
    string? FontFamily,
    int? FontSize);

public sealed record NumberFormatSnapshot(
    string? Type,
    string? Pattern);

public sealed record BordersSnapshot(
    BorderSnapshot? Top,
    BorderSnapshot? Right,
    BorderSnapshot? Bottom,
    BorderSnapshot? Left);

public sealed record BorderSnapshot(
    string? Style,
    int? Width,
    string? Color);

public sealed record MergedRangeSummary(
    string Range,
    int RowCount,
    int ColumnCount);

public sealed record FormattingUpdateInput(
    string Range,
    CellFormatUpdate Format,
    IReadOnlyList<string>? ClearFields = null);

public sealed record CellFormatUpdate(
    bool? Bold = null,
    bool? Italic = null,
    bool? Underline = null,
    bool? Strikethrough = null,
    string? FontFamily = null,
    int? FontSize = null,
    string? TextColor = null,
    string? BackgroundColor = null,
    string? HorizontalAlignment = null,
    string? VerticalAlignment = null,
    string? WrapStrategy = null,
    NumberFormatUpdate? NumberFormat = null,
    BordersUpdate? Borders = null);

public sealed record NumberFormatUpdate(
    string Type,
    string? Pattern = null);

public sealed record BordersUpdate(
    BorderUpdate? Top = null,
    BorderUpdate? Right = null,
    BorderUpdate? Bottom = null,
    BorderUpdate? Left = null);

public sealed record BorderUpdate(
    string Style,
    string? Color = null,
    int? Width = null);

public sealed record FormattingPreviewResult(
    string OperationId,
    string SpreadsheetId,
    DateTimeOffset ExpiresAt,
    int RangeCount,
    int TotalRows,
    int TotalColumns,
    int TotalCells,
    IReadOnlyList<string> Fields,
    IReadOnlyList<FormattingPreviewRange> Ranges);

public sealed record FormattingPreviewRange(
    string Range,
    int RowCount,
    int ColumnCount,
    int CellCount,
    IReadOnlyList<string> Fields);

public sealed record FormattingConfirmResult(
    string OperationId,
    string SpreadsheetId,
    int UpdatedRanges,
    int UpdatedCells,
    IReadOnlyList<string> Ranges,
    IReadOnlyList<string> Fields);
