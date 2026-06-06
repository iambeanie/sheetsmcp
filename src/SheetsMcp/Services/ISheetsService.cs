using SheetsMcp.Models;

namespace SheetsMcp.Services;

public interface ISheetsService
{
    Task<SpreadsheetMetadataResult> GetMetadataAsync(string spreadsheetId, CancellationToken cancellationToken);

    Task<ReadRangeResult> ReadRangeAsync(string spreadsheetId, string range, CancellationToken cancellationToken);

    Task<FormattingReadResult> ReadFormattingAsync(string spreadsheetId, string range, CancellationToken cancellationToken);

    Task<AppendResult> AppendRowsAsync(string spreadsheetId, string rangeOrSheet, IReadOnlyList<IReadOnlyList<object?>> rows, CancellationToken cancellationToken);

    Task<WriteResult> UpdateRangeAsync(string spreadsheetId, string range, IReadOnlyList<IReadOnlyList<object?>> values, CancellationToken cancellationToken);

    Task<BatchConfirmResult> BatchUpdateValuesAsync(string operationId, string spreadsheetId, IReadOnlyList<BatchValueUpdateInput> updates, CancellationToken cancellationToken);

    Task<FormattingConfirmResult> BatchUpdateFormattingAsync(string operationId, string spreadsheetId, IReadOnlyList<FormattingUpdateInput> updates, IReadOnlyList<string> fields, CancellationToken cancellationToken);
}
