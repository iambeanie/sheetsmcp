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
        var service = CreateToolService(sheets);

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
        var service = CreateToolService(new FakeSheetsService());

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
        var service = CreateToolService(sheets, previewStore);
        var preview = service.PreviewBatchUpdate(
            "1MdXEp5BSfUGAAgHD4LPygE9RZXd_nClKsmzBdnaax1c",
            [new BatchValueUpdateInput("Sheet1!A1:A1", [["value"]])]);

        var result = await service.ConfirmBatchUpdateAsync(preview.OperationId, CancellationToken.None);

        Assert.Equal(preview.OperationId, result.OperationId);
        Assert.Single(sheets.BatchUpdates);
    }

    [Fact]
    public async Task ReadFormatting_requires_sheet_qualified_range()
    {
        var service = CreateToolService(new FakeSheetsService());

        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() =>
            service.ReadFormattingAsync("1MdXEp5BSfUGAAgHD4LPygE9RZXd_nClKsmzBdnaax1c", "A1:B2", CancellationToken.None));
    }

    [Fact]
    public void PreviewFormattingUpdate_validates_and_summarizes_fields_without_values()
    {
        var service = CreateToolService(new FakeSheetsService());

        var result = service.PreviewFormattingUpdate(
            "1MdXEp5BSfUGAAgHD4LPygE9RZXd_nClKsmzBdnaax1c",
            [
                new FormattingUpdateInput(
                    "Sheet1!A1:B2",
                    new CellFormatUpdate(Bold: true, BackgroundColor: "#FFCC00", HorizontalAlignment: "center"),
                    ["italic"])
            ]);

        Assert.StartsWith("format_", result.OperationId, StringComparison.Ordinal);
        Assert.Equal(1, result.RangeCount);
        Assert.Equal(4, result.TotalCells);
        Assert.Contains("backgroundColor", result.Fields);
        Assert.Contains("horizontalAlignment", result.Fields);
        Assert.Contains("textFormat.bold", result.Fields);
        Assert.Contains("textFormat.italic", result.Fields);
        Assert.DoesNotContain("#FFCC00", result.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("#FFF")]
    [InlineData("red")]
    [InlineData("#GG0000")]
    public void PreviewFormattingUpdate_rejects_invalid_colors(string color)
    {
        var service = CreateToolService(new FakeSheetsService());

        Assert.Throws<ModelContextProtocol.McpException>(() => service.PreviewFormattingUpdate(
            "1MdXEp5BSfUGAAgHD4LPygE9RZXd_nClKsmzBdnaax1c",
            [new FormattingUpdateInput("Sheet1!A1:A1", new CellFormatUpdate(BackgroundColor: color))]));
    }

    [Fact]
    public void PreviewFormattingUpdate_rejects_noop_updates()
    {
        var service = CreateToolService(new FakeSheetsService());

        Assert.Throws<ModelContextProtocol.McpException>(() => service.PreviewFormattingUpdate(
            "1MdXEp5BSfUGAAgHD4LPygE9RZXd_nClKsmzBdnaax1c",
            [new FormattingUpdateInput("Sheet1!A1:A1", new CellFormatUpdate())]));
    }

    [Fact]
    public async Task ConfirmFormattingUpdate_consumes_preview_and_calls_sheets_service()
    {
        var sheets = new FakeSheetsService();
        var formattingStore = new Preview.InMemoryFormattingPreviewStore();
        var service = CreateToolService(sheets, formattingPreviewStore: formattingStore);
        var preview = service.PreviewFormattingUpdate(
            "1MdXEp5BSfUGAAgHD4LPygE9RZXd_nClKsmzBdnaax1c",
            [new FormattingUpdateInput("Sheet1!A1:A1", new CellFormatUpdate(Bold: true))]);

        var result = await service.ConfirmFormattingUpdateAsync(preview.OperationId, CancellationToken.None);

        Assert.Equal(preview.OperationId, result.OperationId);
        Assert.Single(sheets.FormattingUpdates);
    }

    [Fact]
    public async Task CreateSheetTab_normalizes_title_and_calls_sheets_service()
    {
        var sheets = new FakeSheetsService();
        var service = CreateToolService(sheets);

        var result = await service.CreateSheetTabAsync(
            "1MdXEp5BSfUGAAgHD4LPygE9RZXd_nClKsmzBdnaax1c",
            "FY25/FY26",
            20,
            8,
            CancellationToken.None);

        Assert.Equal("FY25/FY26", result.Title);
        Assert.Equal("created", result.Action);
        Assert.Equal(("FY25/FY26", 20, 8), sheets.CreatedTabs.Single());
    }

    [Fact]
    public async Task RenameSheetTab_resolves_existing_title_and_calls_sheets_service()
    {
        var sheets = new FakeSheetsService();
        var service = CreateToolService(sheets);

        var result = await service.RenameSheetTabAsync(
            "1MdXEp5BSfUGAAgHD4LPygE9RZXd_nClKsmzBdnaax1c",
            "FY25/FY26",
            "FY26/FY27",
            CancellationToken.None);

        Assert.Equal("FY26/FY27", result.Title);
        Assert.Equal((7, "FY26/FY27"), sheets.RenamedTabs.Single());
    }

    [Fact]
    public async Task PreviewDeleteSheetTab_returns_operation_without_deleting()
    {
        var sheets = new FakeSheetsService();
        var service = CreateToolService(sheets);

        var result = await service.PreviewDeleteSheetTabAsync(
            "1MdXEp5BSfUGAAgHD4LPygE9RZXd_nClKsmzBdnaax1c",
            "FY25/FY26",
            CancellationToken.None);

        Assert.StartsWith("delete_sheet_tab_", result.OperationId, StringComparison.Ordinal);
        Assert.Equal(7, result.SheetId);
        Assert.Equal("FY25/FY26", result.Title);
        Assert.Empty(sheets.DeletedTabs);
    }

    [Fact]
    public async Task ConfirmDeleteSheetTab_consumes_preview_and_calls_sheets_service()
    {
        var sheets = new FakeSheetsService();
        var deleteStore = new Preview.InMemoryDeleteSheetTabPreviewStore();
        var service = CreateToolService(sheets, deleteSheetTabPreviewStore: deleteStore);
        var preview = await service.PreviewDeleteSheetTabAsync(
            "1MdXEp5BSfUGAAgHD4LPygE9RZXd_nClKsmzBdnaax1c",
            "FY25/FY26",
            CancellationToken.None);

        var result = await service.ConfirmDeleteSheetTabAsync(preview.OperationId, CancellationToken.None);

        Assert.Equal(preview.OperationId, result.OperationId);
        Assert.Equal("deleted", result.Action);
        Assert.Equal((7, "FY25/FY26"), sheets.DeletedTabs.Single());
    }

    private static SpreadsheetToolService CreateToolService(
        FakeSheetsService sheets,
        Preview.IBatchPreviewStore? batchPreviewStore = null,
        Preview.IFormattingPreviewStore? formattingPreviewStore = null,
        Preview.IDeleteSheetTabPreviewStore? deleteSheetTabPreviewStore = null)
    {
        return new SpreadsheetToolService(
            sheets,
            batchPreviewStore ?? new Preview.InMemoryBatchPreviewStore(),
            formattingPreviewStore ?? new Preview.InMemoryFormattingPreviewStore(),
            deleteSheetTabPreviewStore ?? new Preview.InMemoryDeleteSheetTabPreviewStore(),
            new NoopAuditLogger());
    }

    private sealed class NoopAuditLogger : IAuditLogger
    {
        public Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSheetsService : ISheetsService
    {
        public List<IReadOnlyList<BatchValueUpdateInput>> BatchUpdates { get; } = [];

        public List<IReadOnlyList<FormattingUpdateInput>> FormattingUpdates { get; } = [];

        public List<(string Title, int? RowCount, int? ColumnCount)> CreatedTabs { get; } = [];

        public List<(int SheetId, string NewTitle)> RenamedTabs { get; } = [];

        public List<(int SheetId, string Title)> DeletedTabs { get; } = [];

        public Func<string, string, CancellationToken, Task<ReadRangeResult>> ReadHandler { get; init; } =
            (_, range, _) => Task.FromResult(new ReadRangeResult("spreadsheet", range, 0, 0, []));

        public Task<SpreadsheetMetadataResult> GetMetadataAsync(string spreadsheetId, CancellationToken cancellationToken) =>
            Task.FromResult(new SpreadsheetMetadataResult(
                spreadsheetId,
                "Test",
                [
                    new SheetSummary("FY25/FY26", 7, 1000, 26, false, "GRID"),
                    new SheetSummary("Other", 8, 1000, 26, false, "GRID")
                ]));

        public Task<ReadRangeResult> ReadRangeAsync(string spreadsheetId, string range, CancellationToken cancellationToken) =>
            ReadHandler(spreadsheetId, range, cancellationToken);

        public Task<FormattingReadResult> ReadFormattingAsync(string spreadsheetId, string range, CancellationToken cancellationToken) =>
            Task.FromResult(new FormattingReadResult(spreadsheetId, range, 0, 0, [], []));

        public Task<AppendResult> AppendRowsAsync(string spreadsheetId, string rangeOrSheet, IReadOnlyList<IReadOnlyList<object?>> rows, CancellationToken cancellationToken) =>
            Task.FromResult(new AppendResult(spreadsheetId, rangeOrSheet, rangeOrSheet, rows.Count, rows.Max(row => row.Count), rows.Sum(row => row.Count)));

        public Task<WriteResult> UpdateRangeAsync(string spreadsheetId, string range, IReadOnlyList<IReadOnlyList<object?>> values, CancellationToken cancellationToken) =>
            Task.FromResult(new WriteResult(spreadsheetId, "update_range", range, values.Count, values.Max(row => row.Count), values.Sum(row => row.Count)));

        public Task<BatchConfirmResult> BatchUpdateValuesAsync(string operationId, string spreadsheetId, IReadOnlyList<BatchValueUpdateInput> updates, CancellationToken cancellationToken)
        {
            BatchUpdates.Add(updates);
            return Task.FromResult(new BatchConfirmResult(operationId, spreadsheetId, 1, 1, 1, updates.Select(update => update.Range).ToList()));
        }

        public Task<FormattingConfirmResult> BatchUpdateFormattingAsync(string operationId, string spreadsheetId, IReadOnlyList<FormattingUpdateInput> updates, IReadOnlyList<string> fields, CancellationToken cancellationToken)
        {
            FormattingUpdates.Add(updates);
            return Task.FromResult(new FormattingConfirmResult(operationId, spreadsheetId, updates.Count, 1, updates.Select(update => update.Range).ToList(), fields));
        }

        public Task<SheetTabResult> CreateSheetTabAsync(string spreadsheetId, string title, int? rowCount, int? columnCount, CancellationToken cancellationToken)
        {
            CreatedTabs.Add((title, rowCount, columnCount));
            return Task.FromResult(new SheetTabResult(spreadsheetId, 9, title, "created"));
        }

        public Task<SheetTabResult> RenameSheetTabAsync(string spreadsheetId, int sheetId, string newTitle, CancellationToken cancellationToken)
        {
            RenamedTabs.Add((sheetId, newTitle));
            return Task.FromResult(new SheetTabResult(spreadsheetId, sheetId, newTitle, "renamed"));
        }

        public Task<DeleteSheetTabConfirmResult> DeleteSheetTabAsync(string operationId, string spreadsheetId, int sheetId, string title, CancellationToken cancellationToken)
        {
            DeletedTabs.Add((sheetId, title));
            return Task.FromResult(new DeleteSheetTabConfirmResult(operationId, spreadsheetId, sheetId, title, "deleted"));
        }
    }
}
