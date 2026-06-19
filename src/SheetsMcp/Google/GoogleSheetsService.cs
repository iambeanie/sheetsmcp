using Google;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util;
using SheetsMcp.Errors;
using SheetsMcp.Models;
using SheetsMcp.Parsing;
using SheetsMcp.Services;

namespace SheetsMcp.Google;

public sealed class GoogleSheetsService(ISheetsServiceFactory factory) : ISheetsService
{
    private const string UserEnteredFormatPrefix = "userEnteredFormat.";

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

    public async Task<FormattingReadResult> ReadFormattingAsync(string spreadsheetId, string range, CancellationToken cancellationToken)
    {
        var parsedRange = A1RangeParser.ParseBounded(range);
        if (string.IsNullOrWhiteSpace(parsedRange.SheetName))
        {
            throw ToolError.InvalidInput("Formatting ranges must include a sheet name such as Sheet1!A1:B2.");
        }

        try
        {
            using var service = await CreateServiceAsync(cancellationToken);
            var request = service.Spreadsheets.Get(spreadsheetId);
            request.IncludeGridData = true;
            request.Ranges = new Repeatable<string>([parsedRange.Original]);
            request.Fields = "spreadsheetId,sheets(properties(sheetId,title),merges,data(startRow,startColumn,rowData(values(effectiveFormat))))";

            var spreadsheet = await request.ExecuteAsync(cancellationToken);
            var sheet = FindSheet(spreadsheet, parsedRange.SheetName);
            var data = sheet.Data?.FirstOrDefault();
            var cells = BuildFormattingCells(parsedRange, data, sheet.Merges);
            var mergedRanges = BuildMergedRanges(parsedRange, sheet.Merges);

            return new FormattingReadResult(
                spreadsheet.SpreadsheetId ?? spreadsheetId,
                parsedRange.Original,
                parsedRange.RowCount,
                parsedRange.ColumnCount,
                cells,
                mergedRanges);
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

    public async Task<FormattingConfirmResult> BatchUpdateFormattingAsync(string operationId, string spreadsheetId, IReadOnlyList<FormattingUpdateInput> updates, IReadOnlyList<string> fields, CancellationToken cancellationToken)
    {
        try
        {
            using var service = await CreateServiceAsync(cancellationToken);
            var sheetIds = await GetSheetIdsAsync(service, spreadsheetId, cancellationToken);
            var requests = new List<Request>(updates.Count);
            var ranges = new List<string>(updates.Count);
            var updatedCells = 0;

            foreach (var update in updates)
            {
                var parsedRange = A1RangeParser.ParseBounded(update.Range);
                if (string.IsNullOrWhiteSpace(parsedRange.SheetName))
                {
                    throw ToolError.InvalidInput("Formatting ranges must include a sheet name such as Sheet1!A1:B2.");
                }

                if (!sheetIds.TryGetValue(parsedRange.SheetName, out var sheetId))
                {
                    throw ToolError.InvalidInput($"Sheet '{parsedRange.SheetName}' was not found.");
                }

                ranges.Add(parsedRange.Original);
                updatedCells += parsedRange.CellCount;
                requests.Add(new Request
                {
                    RepeatCell = new RepeatCellRequest
                    {
                        Range = ToGridRange(parsedRange, sheetId),
                        Cell = new CellData
                        {
                            UserEnteredFormat = ToGoogleCellFormat(update.Format)
                        },
                        Fields = ToGoogleFieldMask(GetUpdateFieldPaths(update))
                    }
                });
            }

            var body = new BatchUpdateSpreadsheetRequest
            {
                Requests = requests
            };
            await service.Spreadsheets.BatchUpdate(body, spreadsheetId).ExecuteAsync(cancellationToken);

            return new FormattingConfirmResult(
                operationId,
                spreadsheetId,
                ranges.Count,
                updatedCells,
                ranges,
                fields);
        }
        catch (GoogleApiException ex)
        {
            throw TranslateGoogleException(ex);
        }
    }

    public async Task<SheetTabResult> CreateSheetTabAsync(string spreadsheetId, string title, int? rowCount, int? columnCount, CancellationToken cancellationToken)
    {
        try
        {
            using var service = await CreateServiceAsync(cancellationToken);
            var properties = new SheetProperties
            {
                Title = title
            };

            if (rowCount is not null || columnCount is not null)
            {
                properties.GridProperties = new GridProperties
                {
                    RowCount = rowCount,
                    ColumnCount = columnCount
                };
            }

            var body = new BatchUpdateSpreadsheetRequest
            {
                Requests =
                [
                    new Request
                    {
                        AddSheet = new AddSheetRequest
                        {
                            Properties = properties
                        }
                    }
                ]
            };

            var response = await service.Spreadsheets.BatchUpdate(body, spreadsheetId).ExecuteAsync(cancellationToken);
            var sheetProperties = response.Replies?.FirstOrDefault()?.AddSheet?.Properties;
            return new SheetTabResult(
                spreadsheetId,
                sheetProperties?.SheetId ?? 0,
                sheetProperties?.Title ?? title,
                "created");
        }
        catch (GoogleApiException ex)
        {
            throw TranslateGoogleException(ex);
        }
    }

    public async Task<SheetTabResult> RenameSheetTabAsync(string spreadsheetId, int sheetId, string newTitle, CancellationToken cancellationToken)
    {
        try
        {
            using var service = await CreateServiceAsync(cancellationToken);
            var body = new BatchUpdateSpreadsheetRequest
            {
                Requests =
                [
                    new Request
                    {
                        UpdateSheetProperties = new UpdateSheetPropertiesRequest
                        {
                            Properties = new SheetProperties
                            {
                                SheetId = sheetId,
                                Title = newTitle
                            },
                            Fields = "title"
                        }
                    }
                ]
            };

            await service.Spreadsheets.BatchUpdate(body, spreadsheetId).ExecuteAsync(cancellationToken);
            return new SheetTabResult(
                spreadsheetId,
                sheetId,
                newTitle,
                "renamed");
        }
        catch (GoogleApiException ex)
        {
            throw TranslateGoogleException(ex);
        }
    }

    public async Task<DeleteSheetTabConfirmResult> DeleteSheetTabAsync(string operationId, string spreadsheetId, int sheetId, string title, CancellationToken cancellationToken)
    {
        try
        {
            using var service = await CreateServiceAsync(cancellationToken);
            var body = new BatchUpdateSpreadsheetRequest
            {
                Requests =
                [
                    new Request
                    {
                        DeleteSheet = new DeleteSheetRequest
                        {
                            SheetId = sheetId
                        }
                    }
                ]
            };

            await service.Spreadsheets.BatchUpdate(body, spreadsheetId).ExecuteAsync(cancellationToken);
            return new DeleteSheetTabConfirmResult(operationId, spreadsheetId, sheetId, title, "deleted");
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

    private async Task<Dictionary<string, int>> GetSheetIdsAsync(SheetsService service, string spreadsheetId, CancellationToken cancellationToken)
    {
        var request = service.Spreadsheets.Get(spreadsheetId);
        request.IncludeGridData = false;
        request.Fields = "sheets(properties(sheetId,title))";
        var spreadsheet = await request.ExecuteAsync(cancellationToken);

        return spreadsheet.Sheets
            .Where(sheet => sheet.Properties?.SheetId is not null && !string.IsNullOrWhiteSpace(sheet.Properties.Title))
            .ToDictionary(sheet => sheet.Properties.Title, sheet => sheet.Properties.SheetId!.Value, StringComparer.Ordinal);
    }

    private static Sheet FindSheet(Spreadsheet spreadsheet, string sheetName)
    {
        var sheet = spreadsheet.Sheets?.FirstOrDefault(sheet => string.Equals(sheet.Properties?.Title, sheetName, StringComparison.Ordinal));
        if (sheet is null)
        {
            throw ToolError.InvalidInput($"Sheet '{sheetName}' was not found.");
        }

        return sheet;
    }

    private static IReadOnlyList<IReadOnlyList<CellFormattingResult>> BuildFormattingCells(A1Range parsedRange, GridData? data, IList<GridRange>? merges)
    {
        var rows = new List<IReadOnlyList<CellFormattingResult>>(parsedRange.RowCount);
        var startRow = data?.StartRow ?? parsedRange.StartRow - 1;
        var startColumn = data?.StartColumn ?? parsedRange.StartColumnIndex - 1;

        for (var rowOffset = 0; rowOffset < parsedRange.RowCount; rowOffset++)
        {
            var row = new List<CellFormattingResult>(parsedRange.ColumnCount);
            var dataRowIndex = parsedRange.StartRow - 1 + rowOffset - startRow;
            var rowData = dataRowIndex >= 0 ? data?.RowData?.ElementAtOrDefault(dataRowIndex) : null;

            for (var columnOffset = 0; columnOffset < parsedRange.ColumnCount; columnOffset++)
            {
                var columnIndex = parsedRange.StartColumnIndex - 1 + columnOffset;
                var dataColumnIndex = columnIndex - startColumn;
                var cellData = dataColumnIndex >= 0 ? rowData?.Values?.ElementAtOrDefault(dataColumnIndex) : null;
                row.Add(new CellFormattingResult(
                    A1RangeParser.CellReference(parsedRange.SheetName, parsedRange.StartRow + rowOffset, parsedRange.StartColumnIndex + columnOffset),
                    ToFormatSnapshot(cellData?.EffectiveFormat),
                    IsInMergedRange(parsedRange.StartRow - 1 + rowOffset, columnIndex, merges)));
            }

            rows.Add(row);
        }

        return rows;
    }

    private static IReadOnlyList<MergedRangeSummary> BuildMergedRanges(A1Range parsedRange, IList<GridRange>? merges)
    {
        if (merges is null)
        {
            return [];
        }

        var rangeGrid = ToGridRange(parsedRange, 0);
        return merges
            .Where(merge => RangesOverlap(rangeGrid, merge))
            .Select(merge =>
            {
                var startRow = merge.StartRowIndex ?? 0;
                var endRow = merge.EndRowIndex ?? startRow;
                var startColumn = merge.StartColumnIndex ?? 0;
                var endColumn = merge.EndColumnIndex ?? startColumn;
                var start = A1RangeParser.CellReference(parsedRange.SheetName, startRow + 1, startColumn + 1);
                var end = A1RangeParser.CellReference(parsedRange.SheetName, endRow, endColumn);
                return new MergedRangeSummary($"{start}:{end}", endRow - startRow, endColumn - startColumn);
            })
            .ToList();
    }

    private static bool IsInMergedRange(int zeroBasedRow, int zeroBasedColumn, IList<GridRange>? merges)
    {
        return merges?.Any(merge =>
            zeroBasedRow >= (merge.StartRowIndex ?? 0) &&
            zeroBasedRow < (merge.EndRowIndex ?? 0) &&
            zeroBasedColumn >= (merge.StartColumnIndex ?? 0) &&
            zeroBasedColumn < (merge.EndColumnIndex ?? 0)) ?? false;
    }

    private static bool RangesOverlap(GridRange first, GridRange second)
    {
        return (first.StartRowIndex ?? 0) < (second.EndRowIndex ?? 0) &&
            (first.EndRowIndex ?? 0) > (second.StartRowIndex ?? 0) &&
            (first.StartColumnIndex ?? 0) < (second.EndColumnIndex ?? 0) &&
            (first.EndColumnIndex ?? 0) > (second.StartColumnIndex ?? 0);
    }

    private static CellFormatSnapshot ToFormatSnapshot(CellFormat? format)
    {
        return new CellFormatSnapshot(
            ToTextFormatSnapshot(format?.TextFormat),
            ToHexColor(format?.TextFormat?.ForegroundColorStyle?.RgbColor ?? format?.TextFormat?.ForegroundColor),
            ToHexColor(format?.BackgroundColorStyle?.RgbColor ?? format?.BackgroundColor),
            format?.HorizontalAlignment,
            format?.VerticalAlignment,
            format?.WrapStrategy,
            ToNumberFormatSnapshot(format?.NumberFormat),
            ToBordersSnapshot(format?.Borders));
    }

    private static TextFormatSnapshot? ToTextFormatSnapshot(TextFormat? format)
    {
        return format is null
            ? null
            : new TextFormatSnapshot(
                format.Bold,
                format.Italic,
                format.Underline,
                format.Strikethrough,
                format.FontFamily,
                format.FontSize);
    }

    private static NumberFormatSnapshot? ToNumberFormatSnapshot(NumberFormat? format)
    {
        return format is null ? null : new NumberFormatSnapshot(format.Type, format.Pattern);
    }

    private static BordersSnapshot? ToBordersSnapshot(Borders? borders)
    {
        return borders is null
            ? null
            : new BordersSnapshot(
                ToBorderSnapshot(borders.Top),
                ToBorderSnapshot(borders.Right),
                ToBorderSnapshot(borders.Bottom),
                ToBorderSnapshot(borders.Left));
    }

    private static BorderSnapshot? ToBorderSnapshot(Border? border)
    {
        return border is null ? null : new BorderSnapshot(border.Style, border.Width, ToHexColor(border.ColorStyle?.RgbColor ?? border.Color));
    }

    private static CellFormat ToGoogleCellFormat(CellFormatUpdate update)
    {
        return new CellFormat
        {
            TextFormat = ToGoogleTextFormat(update),
            BackgroundColor = ToGoogleColor(update.BackgroundColor),
            HorizontalAlignment = NormalizeEnum(update.HorizontalAlignment),
            VerticalAlignment = NormalizeEnum(update.VerticalAlignment),
            WrapStrategy = NormalizeEnum(update.WrapStrategy),
            NumberFormat = update.NumberFormat is null
                ? null
                : new NumberFormat
                {
                    Type = NormalizeEnum(update.NumberFormat.Type),
                    Pattern = update.NumberFormat.Pattern
                },
            Borders = ToGoogleBorders(update.Borders)
        };
    }

    private static TextFormat? ToGoogleTextFormat(CellFormatUpdate update)
    {
        if (update.Bold is null &&
            update.Italic is null &&
            update.Underline is null &&
            update.Strikethrough is null &&
            update.FontFamily is null &&
            update.FontSize is null &&
            update.TextColor is null)
        {
            return null;
        }

        return new TextFormat
        {
            Bold = update.Bold,
            Italic = update.Italic,
            Underline = update.Underline,
            Strikethrough = update.Strikethrough,
            FontFamily = update.FontFamily,
            FontSize = update.FontSize,
            ForegroundColor = ToGoogleColor(update.TextColor)
        };
    }

    private static Borders? ToGoogleBorders(BordersUpdate? borders)
    {
        return borders is null
            ? null
            : new Borders
            {
                Top = ToGoogleBorder(borders.Top),
                Right = ToGoogleBorder(borders.Right),
                Bottom = ToGoogleBorder(borders.Bottom),
                Left = ToGoogleBorder(borders.Left)
            };
    }

    private static Border? ToGoogleBorder(BorderUpdate? border)
    {
        return border is null
            ? null
            : new Border
            {
                Style = NormalizeEnum(border.Style),
                Color = ToGoogleColor(border.Color),
                Width = border.Width
            };
    }

    private static GridRange ToGridRange(A1Range range, int sheetId)
    {
        return new GridRange
        {
            SheetId = sheetId,
            StartRowIndex = range.StartRow - 1,
            EndRowIndex = range.EndRow,
            StartColumnIndex = range.StartColumnIndex - 1,
            EndColumnIndex = range.EndColumnIndex
        };
    }

    private static IReadOnlyList<string> GetUpdateFieldPaths(FormattingUpdateInput update)
    {
        var fields = new SortedSet<string>(StringComparer.Ordinal);
        var format = update.Format;

        AddFieldIfSet(fields, format.Bold, "textFormat.bold");
        AddFieldIfSet(fields, format.Italic, "textFormat.italic");
        AddFieldIfSet(fields, format.Underline, "textFormat.underline");
        AddFieldIfSet(fields, format.Strikethrough, "textFormat.strikethrough");
        AddFieldIfSet(fields, format.FontFamily, "textFormat.fontFamily");
        AddFieldIfSet(fields, format.FontSize, "textFormat.fontSize");
        AddFieldIfSet(fields, format.TextColor, "textFormat.foregroundColor");
        AddFieldIfSet(fields, format.BackgroundColor, "backgroundColor");
        AddFieldIfSet(fields, format.HorizontalAlignment, "horizontalAlignment");
        AddFieldIfSet(fields, format.VerticalAlignment, "verticalAlignment");
        AddFieldIfSet(fields, format.WrapStrategy, "wrapStrategy");

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

    private static string ToGoogleFieldMask(IReadOnlyList<string> fields)
    {
        return string.Join(",", fields.Select(field => UserEnteredFormatPrefix + field));
    }

    private static void AddFieldIfSet<T>(ISet<string> fields, T? value, string field)
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
        AddFieldIfSet(fields, border.Color, $"{prefix}.color");
        AddFieldIfSet(fields, border.Width, $"{prefix}.width");
    }

    private static Color? ToGoogleColor(string? hexColor)
    {
        if (hexColor is null)
        {
            return null;
        }

        return new Color
        {
            Red = Convert.ToInt32(hexColor[1..3], 16) / 255f,
            Green = Convert.ToInt32(hexColor[3..5], 16) / 255f,
            Blue = Convert.ToInt32(hexColor[5..7], 16) / 255f
        };
    }

    private static string? ToHexColor(Color? color)
    {
        if (color is null)
        {
            return null;
        }

        var red = ToByte(color.Red);
        var green = ToByte(color.Green);
        var blue = ToByte(color.Blue);
        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    private static int ToByte(float? channel)
    {
        return Math.Clamp((int)Math.Round((channel ?? 0f) * 255, MidpointRounding.AwayFromZero), 0, 255);
    }

    private static string? NormalizeEnum(string? value)
    {
        return value?.Trim().ToUpperInvariant();
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
