using Google.Apis.Sheets.v4;

namespace SheetsMcp.Google;

public interface ISheetsServiceFactory
{
    Task<SheetsService> CreateAsync(CancellationToken cancellationToken);
}
