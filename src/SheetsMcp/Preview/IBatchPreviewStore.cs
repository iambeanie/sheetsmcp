using SheetsMcp.Models;

namespace SheetsMcp.Preview;

public interface IBatchPreviewStore
{
    BatchPreviewOperation Create(string spreadsheetId, IReadOnlyList<BatchValueUpdateInput> updates);

    BatchPreviewOperation Consume(string operationId);
}
