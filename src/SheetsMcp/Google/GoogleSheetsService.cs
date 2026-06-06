using Google;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using SheetsMcp.Errors;
using SheetsMcp.Models;
using SheetsMcp.Parsing;
using SheetsMcp.Services;

namespace SheetsMcp.Google;

public sealed class GoogleSheetsService(ISheetsServiceFactory factory) : ISheetsService
{
    public async Task<SpreadsheetMetadataResult> GetMetadataAsync(string spreadsheetId, CancellationToken cancellationToken)
    {
        try
        {
            using var service = await CreateServiceAsync(cancellationToken);
            var request = service.Spreadsheets.Get(spreadsheetId);
            request.IncludeGridData = false;
            var spreadsheet = await request.ExecuteAsync(cancellationToken);
            var sheets = spreadsheet.Sheets
                .Select(sheet => new SheetSummary(
                    sheet.Properties.Title,
                    sheet.Properties.SheetId,
                    sheet.Properties.GridProperties?.RowCount ?? 0,
                    sheet.Properties.GridProperties?.ColumnCount ?? 0,
                    sheet.Properties.Hidden ?? false,
                    sheet.Properties.SheetType))
                .ToList();

            return new SpreadsheetMetadataResult(spreadsheet.SpreadsheetId, spreadsheet.Properties?.Title, sheets);
        }
        catch (GoogleApiException ex)
        {
            throw TranslateGoogleException(ex);
        }
    }

    public async Task<ReadRangeResult> ReadRangeAsync(string spreadsheetId, string range, CancellationToken cancellationToken)
    {
        try
        {
            using var service = await CreateServiceAsync(cancellationToken);
            var response = await service.Spreadsheets.Values.Get(spreadsheetId, range).ExecuteAsync(cancellationToken);
            var values = ConvertValues(response.Values);
            var columnCount = ValueNormalizer.MaxColumnCount(values);
            return new ReadRangeResult(spreadsheetId, response.Range ?? range, values.Count, columnCount, values);
        }
        catch (GoogleApiException ex)
        {
            throw TranslateGoogleException(ex);
        }
    }

    public async Task<AppendResult> AppendRowsAsync(string spreadsheetId, string rangeOrSheet, IReadOnlyList<IReadOnlyList<object?>> rows, CancellationToken cancellationToken)
    {
        try
        {
            using var service = await CreateServiceAsync(cancellationToken);
            var request = service.Spreadsheets.Values.Append(new ValueRange
            {
                Values = ToGoogleValues(rows)
            }, spreadsheetId, rangeOrSheet);
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            request.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;

            var response = await request.ExecuteAsync(cancellationToken);
            var updates = response.Updates;
            return new AppendResult(
                spreadsheetId,
                rangeOrSheet,
                updates?.UpdatedRange,
                updates?.UpdatedRows ?? 0,
                updates?.UpdatedColumns ?? 0,
                updates?.UpdatedCells ?? 0);
        }
        catch (GoogleApiException ex)
        {
            throw TranslateGoogleException(ex);
        }
    }

    public async Task<WriteResult> UpdateRangeAsync(string spreadsheetId, string range, IReadOnlyList<IReadOnlyList<object?>> values, CancellationToken cancellationToken)
    {
        try
        {
            using var service = await CreateServiceAsync(cancellationToken);
            var request = service.Spreadsheets.Values.Update(new ValueRange
            {
                Values = ToGoogleValues(values)
            }, spreadsheetId, range);
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

            var response = await request.ExecuteAsync(cancellationToken);
            return new WriteResult(
                spreadsheetId,
                "update_range",
                response.UpdatedRange ?? range,
                response.UpdatedRows ?? 0,
                response.UpdatedColumns ?? 0,
                response.UpdatedCells ?? 0);
        }
        catch (GoogleApiException ex)
        {
            throw TranslateGoogleException(ex);
        }
    }

    public async Task<BatchConfirmResult> BatchUpdateValuesAsync(string operationId, string spreadsheetId, IReadOnlyList<BatchValueUpdateInput> updates, CancellationToken cancellationToken)
    {
        try
        {
            using var service = await CreateServiceAsync(cancellationToken);
            var body = new BatchUpdateValuesRequest
            {
                ValueInputOption = "USER_ENTERED",
                Data = updates.Select(update => new ValueRange
                {
                    Range = update.Range,
                    Values = ToGoogleValues(update.Values)
                }).ToList()
            };

            var response = await service.Spreadsheets.Values.BatchUpdate(body, spreadsheetId).ExecuteAsync(cancellationToken);
            var updatedRanges = response.Responses?
                .Select(update => update.UpdatedRange)
                .Where(range => !string.IsNullOrWhiteSpace(range))
                .Cast<string>()
                .ToList() ?? [];

            return new BatchConfirmResult(
                operationId,
                spreadsheetId,
                response.TotalUpdatedRows ?? 0,
                response.TotalUpdatedColumns ?? 0,
                response.TotalUpdatedCells ?? 0,
                updatedRanges);
        }
        catch (GoogleApiException ex)
        {
            throw TranslateGoogleException(ex);
        }
    }

    private async Task<SheetsService> CreateServiceAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await factory.CreateAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw ToolError.OperationFailed(ex.Message);
        }
    }

    private static IReadOnlyList<IReadOnlyList<object?>> ConvertValues(IList<IList<object>>? values)
    {
        return values?
            .Select(row => (IReadOnlyList<object?>)row.Cast<object?>().ToList())
            .ToList() ?? [];
    }

    private static IList<IList<object>> ToGoogleValues(IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        return rows
            .Select(row => (IList<object>)row.Select(value => value ?? string.Empty).ToList())
            .ToList();
    }

    private static Exception TranslateGoogleException(GoogleApiException ex)
    {
        var message = ex.HttpStatusCode switch
        {
            System.Net.HttpStatusCode.Forbidden => "Google Sheets permission denied. Confirm the signed-in Google user has access to the spreadsheet.",
            System.Net.HttpStatusCode.NotFound => "Google Sheets spreadsheet or range was not found.",
            System.Net.HttpStatusCode.BadRequest => "Google Sheets rejected the request. Check the spreadsheet ID, sheet name, and A1 range.",
            System.Net.HttpStatusCode.TooManyRequests => "Google Sheets quota was exceeded. Retry later.",
            _ => "Google Sheets API request failed."
        };

        return ToolError.OperationFailed(message);
    }
}
