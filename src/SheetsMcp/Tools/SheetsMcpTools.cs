using System.ComponentModel;
using ModelContextProtocol.Server;
using SheetsMcp.Models;
using SheetsMcp.Services;

namespace SheetsMcp.Tools;

[McpServerToolType]
public static class SheetsMcpTools
{
    [McpServerTool(Name = "get_spreadsheet_metadata", ReadOnly = true, Destructive = false), Description("Return spreadsheet title, sheets, grid sizes, and basic properties.")]
    public static Task<SpreadsheetMetadataResult> GetSpreadsheetMetadata(
        ISpreadsheetToolService service,
        [Description("Google Sheets URL or spreadsheet ID.")] string spreadsheet,
        CancellationToken cancellationToken) =>
        service.GetMetadataAsync(spreadsheet, cancellationToken);

    [McpServerTool(Name = "read_range", ReadOnly = true, Destructive = false), Description("Read values from a bounded A1 range.")]
    public static Task<ReadRangeResult> ReadRange(
        ISpreadsheetToolService service,
        [Description("Google Sheets URL or spreadsheet ID.")] string spreadsheet,
        [Description("Bounded A1 range such as Sheet1!A1:B10.")] string range,
        CancellationToken cancellationToken) =>
        service.ReadRangeAsync(spreadsheet, range, cancellationToken);

    [McpServerTool(Name = "read_formatting", ReadOnly = true, Destructive = false), Description("Read common cell formatting from a bounded, sheet-qualified A1 range.")]
    public static Task<FormattingReadResult> ReadFormatting(
        ISpreadsheetToolService service,
        [Description("Google Sheets URL or spreadsheet ID.")] string spreadsheet,
        [Description("Sheet-qualified bounded A1 range such as Sheet1!A1:B10.")] string range,
        CancellationToken cancellationToken) =>
        service.ReadFormattingAsync(spreadsheet, range, cancellationToken);

    [McpServerTool(Name = "find_values", ReadOnly = true, Destructive = false), Description("Search provided bounded ranges for matching text.")]
    public static Task<FindValuesResult> FindValues(
        ISpreadsheetToolService service,
        [Description("Google Sheets URL or spreadsheet ID.")] string spreadsheet,
        [Description("One or more bounded A1 ranges to search.")] IReadOnlyList<string> ranges,
        [Description("Text to search for.")] string query,
        [Description("Use case-sensitive matching.")] bool matchCase = false,
        [Description("Require exact cell text match instead of substring matching.")] bool exactMatch = false,
        CancellationToken cancellationToken = default) =>
        service.FindValuesAsync(spreadsheet, ranges, query, matchCase, exactMatch, cancellationToken);

    [McpServerTool(Name = "append_rows", ReadOnly = false, Destructive = false), Description("Append rows to a specific sheet or range.")]
    public static Task<AppendResult> AppendRows(
        ISpreadsheetToolService service,
        [Description("Google Sheets URL or spreadsheet ID.")] string spreadsheet,
        [Description("Sheet name or bounded A1 range to append to.")] string rangeOrSheet,
        [Description("Rows to append. Cell values may be strings, numbers, booleans, or null.")] IReadOnlyList<IReadOnlyList<object?>> rows,
        CancellationToken cancellationToken) =>
        service.AppendRowsAsync(spreadsheet, rangeOrSheet, rows, cancellationToken);

    [McpServerTool(Name = "update_range", ReadOnly = false, Destructive = true, Idempotent = true), Description("Update a bounded A1 range.")]
    public static Task<WriteResult> UpdateRange(
        ISpreadsheetToolService service,
        [Description("Google Sheets URL or spreadsheet ID.")] string spreadsheet,
        [Description("Bounded A1 range such as Sheet1!A1:B10.")] string range,
        [Description("Values to write. Cell values may be strings, numbers, booleans, or null.")] IReadOnlyList<IReadOnlyList<object?>> values,
        CancellationToken cancellationToken) =>
        service.UpdateRangeAsync(spreadsheet, range, values, cancellationToken);

    [McpServerTool(Name = "batch_update_with_preview", ReadOnly = false, Destructive = true), Description("Preview multiple bounded value updates and return a confirmation operation ID.")]
    public static BatchPreviewResult BatchUpdateWithPreview(
        ISpreadsheetToolService service,
        [Description("Google Sheets URL or spreadsheet ID.")] string spreadsheet,
        [Description("Bounded value updates to preview.")] IReadOnlyList<BatchValueUpdateInput> updates) =>
        service.PreviewBatchUpdate(spreadsheet, updates);

    [McpServerTool(Name = "confirm_batch_update", ReadOnly = false, Destructive = true), Description("Apply a previously previewed batch value update.")]
    public static Task<BatchConfirmResult> ConfirmBatchUpdate(
        ISpreadsheetToolService service,
        [Description("Operation ID returned by batch_update_with_preview.")] string operationId,
        CancellationToken cancellationToken) =>
        service.ConfirmBatchUpdateAsync(operationId, cancellationToken);

    [McpServerTool(Name = "format_range_with_preview", ReadOnly = false, Destructive = true), Description("Preview common formatting changes for bounded ranges and return a confirmation operation ID.")]
    public static FormattingPreviewResult FormatRangeWithPreview(
        ISpreadsheetToolService service,
        [Description("Google Sheets URL or spreadsheet ID.")] string spreadsheet,
        [Description("Formatting updates to preview. Ranges must be sheet-qualified bounded A1 ranges.")] IReadOnlyList<FormattingUpdateInput> updates) =>
        service.PreviewFormattingUpdate(spreadsheet, updates);

    [McpServerTool(Name = "confirm_formatting_update", ReadOnly = false, Destructive = true), Description("Apply a previously previewed formatting update.")]
    public static Task<FormattingConfirmResult> ConfirmFormattingUpdate(
        ISpreadsheetToolService service,
        [Description("Operation ID returned by format_range_with_preview.")] string operationId,
        CancellationToken cancellationToken) =>
        service.ConfirmFormattingUpdateAsync(operationId, cancellationToken);

    [McpServerTool(Name = "create_sheet_tab", ReadOnly = false, Destructive = false), Description("Create a new sheet tab in a spreadsheet.")]
    public static Task<SheetTabResult> CreateSheetTab(
        ISpreadsheetToolService service,
        [Description("Google Sheets URL or spreadsheet ID.")] string spreadsheet,
        [Description("Title for the new sheet tab.")] string title,
        [Description("Optional initial row count.")] int? rowCount = null,
        [Description("Optional initial column count.")] int? columnCount = null,
        CancellationToken cancellationToken = default) =>
        service.CreateSheetTabAsync(spreadsheet, title, rowCount, columnCount, cancellationToken);

    [McpServerTool(Name = "rename_sheet_tab", ReadOnly = false, Destructive = true, Idempotent = true), Description("Rename an existing sheet tab.")]
    public static Task<SheetTabResult> RenameSheetTab(
        ISpreadsheetToolService service,
        [Description("Google Sheets URL or spreadsheet ID.")] string spreadsheet,
        [Description("Exact current sheet tab title.")] string sheet,
        [Description("New sheet tab title.")] string newTitle,
        CancellationToken cancellationToken) =>
        service.RenameSheetTabAsync(spreadsheet, sheet, newTitle, cancellationToken);

    [McpServerTool(Name = "delete_sheet_tab_with_preview", ReadOnly = false, Destructive = true), Description("Preview deleting a sheet tab and return a confirmation operation ID.")]
    public static Task<DeleteSheetTabPreviewResult> DeleteSheetTabWithPreview(
        ISpreadsheetToolService service,
        [Description("Google Sheets URL or spreadsheet ID.")] string spreadsheet,
        [Description("Exact sheet tab title to delete.")] string sheet,
        CancellationToken cancellationToken) =>
        service.PreviewDeleteSheetTabAsync(spreadsheet, sheet, cancellationToken);

    [McpServerTool(Name = "confirm_delete_sheet_tab", ReadOnly = false, Destructive = true), Description("Delete a sheet tab from a previously previewed operation.")]
    public static Task<DeleteSheetTabConfirmResult> ConfirmDeleteSheetTab(
        ISpreadsheetToolService service,
        [Description("Operation ID returned by delete_sheet_tab_with_preview.")] string operationId,
        CancellationToken cancellationToken) =>
        service.ConfirmDeleteSheetTabAsync(operationId, cancellationToken);
}
