using SheetsMcp.Models;

namespace SheetsMcp.Preview;

public interface IFormattingPreviewStore
{
    FormattingPreviewOperation Create(string spreadsheetId, IReadOnlyList<FormattingUpdateInput> updates, IReadOnlyList<string> fields);

    FormattingPreviewOperation Consume(string operationId);
}
