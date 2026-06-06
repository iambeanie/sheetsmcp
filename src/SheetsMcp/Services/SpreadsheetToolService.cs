using SheetsMcp.Models;
using SheetsMcp.Parsing;
using SheetsMcp.Preview;
using System.Text.RegularExpressions;

namespace SheetsMcp.Services;

public interface ISpreadsheetToolService
{
    Task<SpreadsheetMetadataResult> GetMetadataAsync(string spreadsheet, CancellationToken cancellationToken);

    Task<ReadRangeResult> ReadRangeAsync(string spreadsheet, string range, CancellationToken cancellationToken);

    Task<FormattingReadResult> ReadFormattingAsync(string spreadsheet, string range, CancellationToken cancellationToken);

    Task<FindValuesResult> FindValuesAsync(string spreadsheet, IReadOnlyList<string> ranges, string query, bool matchCase, bool exactMatch, CancellationToken cancellationToken);

    Task<AppendResult> AppendRowsAsync(string spreadsheet, string rangeOrSheet, IReadOnlyList<IReadOnlyList<object?>> rows, CancellationToken cancellationToken);

    Task<WriteResult> UpdateRangeAsync(string spreadsheet, string range, IReadOnlyList<IReadOnlyList<object?>> values, CancellationToken cancellationToken);

    BatchPreviewResult PreviewBatchUpdate(string spreadsheet, IReadOnlyList<BatchValueUpdateInput> updates);

    Task<BatchConfirmResult> ConfirmBatchUpdateAsync(string operationId, CancellationToken cancellationToken);

    FormattingPreviewResult PreviewFormattingUpdate(string spreadsheet, IReadOnlyList<FormattingUpdateInput> updates);

    Task<FormattingConfirmResult> ConfirmFormattingUpdateAsync(string operationId, CancellationToken cancellationToken);
}

public sealed class SpreadsheetToolService(
    ISheetsService sheetsService,
    IBatchPreviewStore previewStore,
    IFormattingPreviewStore formattingPreviewStore,
    IAuditLogger auditLogger) : ISpreadsheetToolService
{
    private static readonly Regex HexColorPattern = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);
    private static readonly HashSet<string> HorizontalAlignments = new(StringComparer.OrdinalIgnoreCase) { "LEFT", "CENTER", "RIGHT" };
    private static readonly HashSet<string> VerticalAlignments = new(StringComparer.OrdinalIgnoreCase) { "TOP", "MIDDLE", "BOTTOM" };
    private static readonly HashSet<string> WrapStrategies = new(StringComparer.OrdinalIgnoreCase) { "OVERFLOW_CELL", "LEGACY_WRAP", "CLIP", "WRAP" };
    private static readonly HashSet<string> NumberFormatTypes = new(StringComparer.OrdinalIgnoreCase) { "TEXT", "NUMBER", "PERCENT", "CURRENCY", "DATE", "TIME", "DATE_TIME", "SCIENTIFIC" };
    private static readonly HashSet<string> BorderStyles = new(StringComparer.OrdinalIgnoreCase)
    {
        "DOTTED", "DASHED", "SOLID", "SOLID_MEDIUM", "SOLID_THICK", "DOUBLE", "NONE"
    };
    private static readonly Dictionary<string, string> ClearFieldMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bold"] = "textFormat.bold",
        ["italic"] = "textFormat.italic",
        ["underline"] = "textFormat.underline",
        ["strikethrough"] = "textFormat.strikethrough",
        ["fontFamily"] = "textFormat.fontFamily",
        ["fontSize"] = "textFormat.fontSize",
        ["textColor"] = "textFormat.foregroundColor",
        ["backgroundColor"] = "backgroundColor",
        ["horizontalAlignment"] = "horizontalAlignment",
        ["verticalAlignment"] = "verticalAlignment",
        ["wrapStrategy"] = "wrapStrategy",
        ["numberFormat"] = "numberFormat",
        ["borders"] = "borders",
        ["borderTop"] = "borders.top",
        ["borderRight"] = "borders.right",
        ["borderBottom"] = "borders.bottom",
        ["borderLeft"] = "borders.left"
    };

    public Task<SpreadsheetMetadataResult> GetMetadataAsync(string spreadsheet, CancellationToken cancellationToken)
    {
        return sheetsService.GetMetadataAsync(SpreadsheetReference.Normalize(spreadsheet), cancellationToken);
    }

    public Task<ReadRangeResult> ReadRangeAsync(string spreadsheet, string range, CancellationToken cancellationToken)
    {
        var spreadsheetId = SpreadsheetReference.Normalize(spreadsheet);
        var parsedRange = A1RangeParser.ParseBounded(range);
        return sheetsService.ReadRangeAsync(spreadsheetId, parsedRange.Original, cancellationToken);
    }

    public Task<FormattingReadResult> ReadFormattingAsync(string spreadsheet, string range, CancellationToken cancellationToken)
    {
        var spreadsheetId = SpreadsheetReference.Normalize(spreadsheet);
        var parsedRange = ParseSheetQualifiedRange(range);
        return sheetsService.ReadFormattingAsync(spreadsheetId, parsedRange.Original, cancellationToken);
    }

    public async Task<FindValuesResult> FindValuesAsync(string spreadsheet, IReadOnlyList<string> ranges, string query, bool matchCase, bool exactMatch, CancellationToken cancellationToken)
    {
        var spreadsheetId = SpreadsheetReference.Normalize(spreadsheet);
        if (ranges.Count == 0)
        {
            throw Errors.ToolError.InvalidInput("At least one range is required.");
        }

        if (string.IsNullOrEmpty(query))
        {
            throw Errors.ToolError.InvalidInput("A non-empty query is required.");
        }

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var matches = new List<FindValueMatch>();

        foreach (var range in ranges)
        {
            var parsedRange = A1RangeParser.ParseBounded(range);
            var result = await sheetsService.ReadRangeAsync(spreadsheetId, parsedRange.Original, cancellationToken);
            for (var rowIndex = 0; rowIndex < result.Values.Count; rowIndex++)
            {
                var row = result.Values[rowIndex];
                for (var columnOffset = 0; columnOffset < row.Count; columnOffset++)
                {
                    var text = Convert.ToString(row[columnOffset], System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                    var isMatch = exactMatch
                        ? text.Equals(query, comparison)
                        : text.Contains(query, comparison);
                    if (isMatch)
                    {
                        matches.Add(new FindValueMatch(
                            A1RangeParser.CellReference(parsedRange.SheetName, parsedRange.StartRow + rowIndex, parsedRange.StartColumnIndex + columnOffset),
                            text));
                    }
                }
            }
        }

        return new FindValuesResult(spreadsheetId, query, matches.Count, matches);
    }

    public async Task<AppendResult> AppendRowsAsync(string spreadsheet, string rangeOrSheet, IReadOnlyList<IReadOnlyList<object?>> rows, CancellationToken cancellationToken)
    {
        var spreadsheetId = SpreadsheetReference.Normalize(spreadsheet);
        var target = A1RangeParser.NormalizeRangeOrSheet(rangeOrSheet);
        var normalizedRows = ValueNormalizer.NormalizeRows(rows, nameof(rows));
        var columnCount = ValueNormalizer.MaxColumnCount(normalizedRows);

        try
        {
            var result = await sheetsService.AppendRowsAsync(spreadsheetId, target, normalizedRows, cancellationToken);
            await AuditAsync("append_rows", spreadsheetId, target, "append", normalizedRows.Count, columnCount, normalizedRows.Sum(row => row.Count), true, cancellationToken);
            return result;
        }
        catch
        {
            await AuditAsync("append_rows", spreadsheetId, target, "append", normalizedRows.Count, columnCount, normalizedRows.Sum(row => row.Count), false, cancellationToken);
            throw;
        }
    }

    public async Task<WriteResult> UpdateRangeAsync(string spreadsheet, string range, IReadOnlyList<IReadOnlyList<object?>> values, CancellationToken cancellationToken)
    {
        var spreadsheetId = SpreadsheetReference.Normalize(spreadsheet);
        var parsedRange = A1RangeParser.ParseBounded(range);
        var normalizedValues = ValueNormalizer.NormalizeRows(values, nameof(values));
        var columnCount = ValueNormalizer.MaxColumnCount(normalizedValues);

        try
        {
            var result = await sheetsService.UpdateRangeAsync(spreadsheetId, parsedRange.Original, normalizedValues, cancellationToken);
            await AuditAsync("update_range", spreadsheetId, parsedRange.Original, "update", normalizedValues.Count, columnCount, normalizedValues.Sum(row => row.Count), true, cancellationToken);
            return result;
        }
        catch
        {
            await AuditAsync("update_range", spreadsheetId, parsedRange.Original, "update", normalizedValues.Count, columnCount, normalizedValues.Sum(row => row.Count), false, cancellationToken);
            throw;
        }
    }

    public BatchPreviewResult PreviewBatchUpdate(string spreadsheet, IReadOnlyList<BatchValueUpdateInput> updates)
    {
        var spreadsheetId = SpreadsheetReference.Normalize(spreadsheet);
        if (updates.Count == 0)
        {
            throw Errors.ToolError.InvalidInput("At least one batch update is required.");
        }

        var normalizedUpdates = new List<BatchValueUpdateInput>(updates.Count);
        var previewRanges = new List<BatchPreviewRange>(updates.Count);

        foreach (var update in updates)
        {
            var parsedRange = A1RangeParser.ParseBounded(update.Range);
            var normalizedValues = ValueNormalizer.NormalizeRows(update.Values, nameof(update.Values));
            var rowCount = normalizedValues.Count;
            var columnCount = ValueNormalizer.MaxColumnCount(normalizedValues);
            var cellCount = normalizedValues.Sum(row => row.Count);
            normalizedUpdates.Add(new BatchValueUpdateInput(parsedRange.Original, normalizedValues));
            previewRanges.Add(new BatchPreviewRange(parsedRange.Original, rowCount, columnCount, cellCount));
        }

        var operation = previewStore.Create(spreadsheetId, normalizedUpdates);
        return new BatchPreviewResult(
            operation.OperationId,
            spreadsheetId,
            operation.ExpiresAt,
            previewRanges.Count,
            previewRanges.Sum(range => range.RowCount),
            previewRanges.Max(range => range.ColumnCount),
            previewRanges.Sum(range => range.CellCount),
            previewRanges);
    }

    public async Task<BatchConfirmResult> ConfirmBatchUpdateAsync(string operationId, CancellationToken cancellationToken)
    {
        var operation = previewStore.Consume(operationId);
        var rowCount = operation.Updates.Sum(update => update.Values.Count);
        var columnCount = operation.Updates.Select(update => ValueNormalizer.MaxColumnCount(update.Values)).DefaultIfEmpty(0).Max();
        var cellCount = operation.Updates.Sum(update => update.Values.Sum(row => row.Count));
        var target = string.Join(",", operation.Updates.Select(update => update.Range));

        try
        {
            var result = await sheetsService.BatchUpdateValuesAsync(operation.OperationId, operation.SpreadsheetId, operation.Updates, cancellationToken);
            await AuditAsync("confirm_batch_update", operation.SpreadsheetId, target, "batch-update", rowCount, columnCount, cellCount, true, cancellationToken);
            return result;
        }
        catch
        {
            await AuditAsync("confirm_batch_update", operation.SpreadsheetId, target, "batch-update", rowCount, columnCount, cellCount, false, cancellationToken);
            throw;
        }
    }

    public FormattingPreviewResult PreviewFormattingUpdate(string spreadsheet, IReadOnlyList<FormattingUpdateInput> updates)
    {
        var spreadsheetId = SpreadsheetReference.Normalize(spreadsheet);
        if (updates.Count == 0)
        {
            throw Errors.ToolError.InvalidInput("At least one formatting update is required.");
        }

        var normalizedUpdates = new List<FormattingUpdateInput>(updates.Count);
        var previewRanges = new List<FormattingPreviewRange>(updates.Count);
        var allFields = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var update in updates)
        {
            var parsedRange = ParseSheetQualifiedRange(update.Range);
            var normalizedUpdate = NormalizeFormattingUpdate(parsedRange.Original, update);
            var fields = GetFormattingFields(normalizedUpdate);
            if (fields.Count == 0)
            {
                throw Errors.ToolError.InvalidInput("Each formatting update must set or clear at least one supported field.");
            }

            foreach (var field in fields)
            {
                allFields.Add(field);
            }

            normalizedUpdates.Add(normalizedUpdate);
            previewRanges.Add(new FormattingPreviewRange(
                parsedRange.Original,
                parsedRange.RowCount,
                parsedRange.ColumnCount,
                parsedRange.CellCount,
                fields));
        }

        var fieldsList = allFields.ToList();
        var operation = formattingPreviewStore.Create(spreadsheetId, normalizedUpdates, fieldsList);
        return new FormattingPreviewResult(
            operation.OperationId,
            spreadsheetId,
            operation.ExpiresAt,
            previewRanges.Count,
            previewRanges.Sum(range => range.RowCount),
            previewRanges.Max(range => range.ColumnCount),
            previewRanges.Sum(range => range.CellCount),
            fieldsList,
            previewRanges);
    }

    public async Task<FormattingConfirmResult> ConfirmFormattingUpdateAsync(string operationId, CancellationToken cancellationToken)
    {
        var operation = formattingPreviewStore.Consume(operationId);
        var parsedRanges = operation.Updates.Select(update => A1RangeParser.ParseBounded(update.Range)).ToList();
        var rowCount = parsedRanges.Sum(range => range.RowCount);
        var columnCount = parsedRanges.Select(range => range.ColumnCount).DefaultIfEmpty(0).Max();
        var cellCount = parsedRanges.Sum(range => range.CellCount);
        var target = string.Join(",", operation.Updates.Select(update => update.Range));

        try
        {
            var result = await sheetsService.BatchUpdateFormattingAsync(
                operation.OperationId,
                operation.SpreadsheetId,
                operation.Updates,
                operation.Fields,
                cancellationToken);
            await AuditAsync("confirm_formatting_update", operation.SpreadsheetId, target, "format-update", rowCount, columnCount, cellCount, true, cancellationToken);
            return result;
        }
        catch
        {
            await AuditAsync("confirm_formatting_update", operation.SpreadsheetId, target, "format-update", rowCount, columnCount, cellCount, false, cancellationToken);
            throw;
        }
    }

    private Task AuditAsync(string tool, string spreadsheetId, string target, string writeType, int rowCount, int columnCount, int cellCount, bool success, CancellationToken cancellationToken)
    {
        return auditLogger.WriteAsync(new AuditLogEntry(
            DateTimeOffset.UtcNow,
            tool,
            spreadsheetId,
            target,
            writeType,
            rowCount,
            columnCount,
            cellCount,
            success), cancellationToken);
    }

    private static A1Range ParseSheetQualifiedRange(string range)
    {
        var parsedRange = A1RangeParser.ParseBounded(range);
        if (string.IsNullOrWhiteSpace(parsedRange.SheetName))
        {
            throw Errors.ToolError.InvalidInput("Formatting ranges must include a sheet name such as Sheet1!A1:B2.");
        }

        return parsedRange;
    }

    private static FormattingUpdateInput NormalizeFormattingUpdate(string normalizedRange, FormattingUpdateInput update)
    {
        var format = update.Format ?? throw Errors.ToolError.InvalidInput("A formatting update requires a format object.");
        ValidateColor(format.TextColor, "textColor");
        ValidateColor(format.BackgroundColor, "backgroundColor");
        ValidateEnum(format.HorizontalAlignment, HorizontalAlignments, "horizontalAlignment");
        ValidateEnum(format.VerticalAlignment, VerticalAlignments, "verticalAlignment");
        ValidateEnum(format.WrapStrategy, WrapStrategies, "wrapStrategy");

        if (format.FontSize is <= 0 or > 400)
        {
            throw Errors.ToolError.InvalidInput("fontSize must be between 1 and 400.");
        }

        if (format.NumberFormat is not null)
        {
            ValidateEnum(format.NumberFormat.Type, NumberFormatTypes, "numberFormat.type");
        }

        ValidateBorder(format.Borders?.Top, "borders.top");
        ValidateBorder(format.Borders?.Right, "borders.right");
        ValidateBorder(format.Borders?.Bottom, "borders.bottom");
        ValidateBorder(format.Borders?.Left, "borders.left");

        var clearFields = update.ClearFields?
            .Select(field => field?.Trim() ?? string.Empty)
            .Where(field => field.Length > 0)
            .Select(field =>
            {
                if (!ClearFieldMap.TryGetValue(field, out var normalized))
                {
                    throw Errors.ToolError.InvalidInput($"Unsupported clear field '{field}'.");
                }

                return normalized;
            })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return new FormattingUpdateInput(normalizedRange, format, clearFields);
    }

    private static IReadOnlyList<string> GetFormattingFields(FormattingUpdateInput update)
    {
        var fields = new SortedSet<string>(StringComparer.Ordinal);
        var format = update.Format;

        AddIfSet(fields, format.Bold, "textFormat.bold");
        AddIfSet(fields, format.Italic, "textFormat.italic");
        AddIfSet(fields, format.Underline, "textFormat.underline");
        AddIfSet(fields, format.Strikethrough, "textFormat.strikethrough");
        AddIfSet(fields, format.FontFamily, "textFormat.fontFamily");
        AddIfSet(fields, format.FontSize, "textFormat.fontSize");
        AddIfSet(fields, format.TextColor, "textFormat.foregroundColor");
        AddIfSet(fields, format.BackgroundColor, "backgroundColor");
        AddIfSet(fields, format.HorizontalAlignment, "horizontalAlignment");
        AddIfSet(fields, format.VerticalAlignment, "verticalAlignment");
        AddIfSet(fields, format.WrapStrategy, "wrapStrategy");

        if (format.NumberFormat is not null)
        {
            fields.Add("numberFormat");
        }

        AddBorderFields(fields, format.Borders?.Top, "borders.top");
        AddBorderFields(fields, format.Borders?.Right, "borders.right");
        AddBorderFields(fields, format.Borders?.Bottom, "borders.bottom");
        AddBorderFields(fields, format.Borders?.Left, "borders.left");

        foreach (var clearField in update.ClearFields ?? [])
        {
            fields.Add(clearField);
        }

        return fields.ToList();
    }

    private static void ValidateBorder(BorderUpdate? border, string name)
    {
        if (border is null)
        {
            return;
        }

        ValidateEnum(border.Style, BorderStyles, $"{name}.style");
        ValidateColor(border.Color, $"{name}.color");
        if (border.Width is < 0 or > 36)
        {
            throw Errors.ToolError.InvalidInput($"{name}.width must be between 0 and 36.");
        }
    }

    private static void ValidateColor(string? color, string name)
    {
        if (color is not null && !HexColorPattern.IsMatch(color))
        {
            throw Errors.ToolError.InvalidInput($"{name} must be a #RRGGBB color.");
        }
    }

    private static void ValidateEnum(string? value, HashSet<string> allowedValues, string name)
    {
        if (value is not null && !allowedValues.Contains(value))
        {
            throw Errors.ToolError.InvalidInput($"{name} has an unsupported value.");
        }
    }

    private static void AddIfSet<T>(ISet<string> fields, T? value, string field)
    {
        if (value is not null)
        {
            fields.Add(field);
        }
    }

    private static void AddBorderFields(ISet<string> fields, BorderUpdate? border, string prefix)
    {
        if (border is null)
        {
            return;
        }

        fields.Add($"{prefix}.style");
        AddIfSet(fields, border.Color, $"{prefix}.color");
        AddIfSet(fields, border.Width, $"{prefix}.width");
    }
}
