namespace SheetsMcp.Preview;

public interface IDeleteSheetTabPreviewStore
{
    DeleteSheetTabPreviewOperation Create(string spreadsheetId, int sheetId, string title);

    DeleteSheetTabPreviewOperation Consume(string operationId);
}
