using SheetsMcp.Models;
using SheetsMcp.Parsing;
using SheetsMcp.Preview;

namespace SheetsMcp.Services;

public interface ISpreadsheetToolService
{
    Task<SpreadsheetMetadataResult> GetMetadataAsync(string spreadsheet, CancellationToken cancellationToken);

    Task<ReadRangeResult> ReadRangeAsync(string spreadsheet, string range, CancellationToken cancellationToken);

    Task<FindValuesResult> FindValuesAsync(string spreadsheet, IReadOnlyList<string> ranges, string query, bool matchCase, bool exactMatch, CancellationToken cancellationToken);

    Task<AppendResult> AppendRowsAsync(string spreadsheet, string rangeOrSheet, IReadOnlyList<IReadOnlyList<object?>> rows, CancellationToken cancellationToken);

    Task<WriteResult> UpdateRangeAsync(string spreadsheet, string range, IReadOnlyList<IReadOnlyList<object?>> values, CancellationToken cancellationToken);

    BatchPreviewResult PreviewBatchUpdate(string spreadsheet, IReadOnlyList<BatchValueUpdateInput> updates);

    Task<BatchConfirmResult> ConfirmBatchUpdateAsync(string operationId, CancellationToken cancellationToken);
}

public sealed class SpreadsheetToolService(
    ISheetsService sheetsService,
    IBatchPreviewStore previewStore,
    IAuditLogger auditLogger) : ISpreadsheetToolService
{
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
}
