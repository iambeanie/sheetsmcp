using Google.Apis.Services;
using Google.Apis.Sheets.v4;

namespace SheetsMcp.Google;

public sealed class GoogleSheetsServiceFactory(GoogleOAuthCredentialProvider credentialProvider) : ISheetsServiceFactory
{
    public async Task<SheetsService> CreateAsync(CancellationToken cancellationToken)
    {
        var credential = await credentialProvider.GetCachedCredentialAsync(cancellationToken);
        return new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "SheetsMCP"
        });
    }
}
