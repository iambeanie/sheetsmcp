using Google.Apis.Sheets.v4;

namespace SheetsMcp.Google;

public interface ISheetsServiceFactory
{
    SheetsService Create();
}
