using SheetsMcp.Models;
using SheetsMcp.Services;

namespace SheetsMcp.Tests;

public sealed class SpreadsheetToolServiceTests
{
    [Fact]
    public async Task FindValues_searches_only_requested_ranges()
    {
        var sheets = new FakeSheetsService
        {
            ReadHandler = (_, range, _) => Task.FromResult(new ReadRangeResult(
                "spreadsheet",
                range,
                2,
                2,
                [
                    ["Alpha", "Beta"],
                    ["gamma", "alphabet"]
                ]))
        };
        var service = new SpreadsheetToolService(sheets, new Preview.InMemoryBatchPreviewStore(), new NoopAuditLogger());

        var result = await service.FindValuesAsync(
            "1MdXEp5BSfUGAAgHD4LPygE9RZXd_nClKsmzBdnaax1c",
            ["'Data Sheet'!A1:B2"],
            "alpha",
            matchCase: false,
            exactMatch: false,
            CancellationToken.None);

        Assert.Equal(2, result.MatchCount);
        Assert.Equal("'Data Sheet'!A1", result.Matches[0].Range);
        Assert.Equal("'Data Sheet'!B2", result.Matches[1].Range);
    }

    [Fact]
    public void PreviewBatchUpdate_normalizes_updates_and_summarizes_without_values()
    {
        var service = new SpreadsheetToolService(new FakeSheetsService(), new Preview.InMemoryBatchPreviewStore(), new NoopAuditLogger());

        var result = service.PreviewBatchUpdate(
            "1MdXEp5BSfUGAAgHD4LPygE9RZXd_nClKsmzBdnaax1c",
            [
                new BatchValueUpdateInput("Sheet1!A1:B2", [["one", "two"], ["three", "four"]]),
                new BatchValueUpdateInput("'Other Sheet'!C3:C3", [["five"]])
            ]);

        Assert.StartsWith("batch_", result.OperationId, StringComparison.Ordinal);
        Assert.Equal(2, result.RangeCount);
        Assert.Equal(3, result.TotalRows);
        Assert.Equal(2, result.TotalColumns);
        Assert.Equal(5, result.TotalCells);
        Assert.DoesNotContain("one", result.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmBatchUpdate_consumes_preview_and_calls_sheets_service()
    {
        var sheets = new FakeSheetsService();
        var previewStore = new Preview.InMemoryBatchPreviewStore();
        var service = new SpreadsheetToolService(sheets, previewStore, new NoopAuditLogger());
        var preview = service.PreviewBatchUpdate(
            "1MdXEp5BSfUGAAgHD4LPygE9RZXd_nClKsmzBdnaax1c",
            [new BatchValueUpdateInput("Sheet1!A1:A1", [["value"]])]);

        var result = await service.ConfirmBatchUpdateAsync(preview.OperationId, CancellationToken.None);

        Assert.Equal(preview.OperationId, result.OperationId);
        Assert.Single(sheets.BatchUpdates);
    }

    private sealed class NoopAuditLogger : IAuditLogger
    {
        public Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSheetsService : ISheetsService
    {
        public List<IReadOnlyList<BatchValueUpdateInput>> BatchUpdates { get; } = [];

        public Func<string, string, CancellationToken, Task<ReadRangeResult>> ReadHandler { get; init; } =
            (_, range, _) => Task.FromResult(new ReadRangeResult("spreadsheet", range, 0, 0, []));

        public Task<SpreadsheetMetadataResult> GetMetadataAsync(string spreadsheetId, CancellationToken cancellationToken) =>
            Task.FromResult(new SpreadsheetMetadataResult(spreadsheetId, "Test", []));

        public Task<ReadRangeResult> ReadRangeAsync(string spreadsheetId, string range, CancellationToken cancellationToken) =>
            ReadHandler(spreadsheetId, range, cancellationToken);

        public Task<AppendResult> AppendRowsAsync(string spreadsheetId, string rangeOrSheet, IReadOnlyList<IReadOnlyList<object?>> rows, CancellationToken cancellationToken) =>
            Task.FromResult(new AppendResult(spreadsheetId, rangeOrSheet, rangeOrSheet, rows.Count, rows.Max(row => row.Count), rows.Sum(row => row.Count)));

        public Task<WriteResult> UpdateRangeAsync(string spreadsheetId, string range, IReadOnlyList<IReadOnlyList<object?>> values, CancellationToken cancellationToken) =>
            Task.FromResult(new WriteResult(spreadsheetId, "update_range", range, values.Count, values.Max(row => row.Count), values.Sum(row => row.Count)));

        public Task<BatchConfirmResult> BatchUpdateValuesAsync(string operationId, string spreadsheetId, IReadOnlyList<BatchValueUpdateInput> updates, CancellationToken cancellationToken)
        {
            BatchUpdates.Add(updates);
            return Task.FromResult(new BatchConfirmResult(operationId, spreadsheetId, 1, 1, 1, updates.Select(update => update.Range).ToList()));
        }
    }
}
