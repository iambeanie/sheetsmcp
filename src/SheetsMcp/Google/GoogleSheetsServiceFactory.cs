using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using SheetsMcp.Configuration;

namespace SheetsMcp.Google;

public sealed class GoogleSheetsServiceFactory(SheetsMcpOptions options) : ISheetsServiceFactory
{
    private readonly Lazy<GoogleCredential> _credential = new(() =>
        CredentialFactory
            .FromFile<ServiceAccountCredential>(options.CredentialsPath)
            .ToGoogleCredential()
            .CreateScoped(SheetsService.Scope.Spreadsheets));

    public SheetsService Create()
    {
        return new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = _credential.Value,
            ApplicationName = "SheetsMCP"
        });
    }
}
