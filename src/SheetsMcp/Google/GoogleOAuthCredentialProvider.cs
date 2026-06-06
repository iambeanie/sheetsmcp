using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;
using SheetsMcp.Configuration;

namespace SheetsMcp.Google;

public sealed class GoogleOAuthCredentialProvider(SheetsMcpOptions options)
{
    private static readonly string[] Scopes = [SheetsService.Scope.Spreadsheets];

    public async Task<UserCredential> AuthorizeAsync(CancellationToken cancellationToken)
    {
        options.ValidateOAuthClientConfigExists();

        await using var stream = File.OpenRead(options.OAuthClientConfigPath);
        var secrets = GoogleClientSecrets.FromStream(stream).Secrets;
        return await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            Scopes,
            SheetsMcpOptions.DefaultOAuthUserId,
            cancellationToken,
            CreateDataStore());
    }

    public async Task<UserCredential> GetCachedCredentialAsync(CancellationToken cancellationToken)
    {
        options.ValidateOAuthClientConfigExists();

        var flow = await CreateFlowAsync(cancellationToken);
        var token = await flow.LoadTokenAsync(SheetsMcpOptions.DefaultOAuthUserId, cancellationToken);
        if (token is null)
        {
            throw new InvalidOperationException("Google OAuth is not configured. Run 'sheetsmcp auth login' before using Sheets tools.");
        }

        return new UserCredential(flow, SheetsMcpOptions.DefaultOAuthUserId, token);
    }

    public async Task<GoogleOAuthStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(options.OAuthClientConfigPath))
        {
            return new GoogleOAuthStatus(false, false, "OAuth client config was not found.");
        }

        var flow = await CreateFlowAsync(cancellationToken);
        var token = await flow.LoadTokenAsync(SheetsMcpOptions.DefaultOAuthUserId, cancellationToken);
        if (token is null)
        {
            return new GoogleOAuthStatus(true, false, "No cached user token was found.");
        }

        var credential = new UserCredential(flow, SheetsMcpOptions.DefaultOAuthUserId, token);
        var refreshed = await credential.RefreshTokenAsync(cancellationToken);
        return new GoogleOAuthStatus(true, refreshed || !string.IsNullOrWhiteSpace(credential.Token.AccessToken), "OAuth token cache is available.");
    }

    public Task LogoutAsync()
    {
        if (Directory.Exists(options.OAuthTokenStorePath))
        {
            Directory.Delete(options.OAuthTokenStorePath, recursive: true);
        }

        return Task.CompletedTask;
    }

    private async Task<GoogleAuthorizationCodeFlow> CreateFlowAsync(CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(options.OAuthClientConfigPath);
        var secrets = GoogleClientSecrets.FromStream(stream).Secrets;
        return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = secrets,
            Scopes = Scopes,
            DataStore = CreateDataStore()
        });
    }

    private FileDataStore CreateDataStore()
    {
        return new FileDataStore(options.OAuthTokenStorePath, fullPath: true);
    }
}

public sealed record GoogleOAuthStatus(bool ClientConfigExists, bool TokenAvailable, string Message);
