# Security

SheetsMCP uses a Google service account to access spreadsheets that have been directly shared with that service-account email address.

## Service-Account Credentials

Service-account JSON keys are sensitive credentials. They allow the app to act as the service account for the scopes granted by the application.

Rules:

- Do not commit service-account JSON files.
- Do not paste service-account JSON into MCP client config.
- Do not log credential paths together with credential contents.
- Do not log private keys, access tokens, or raw Google auth responses.
- Store the JSON key in a user-controlled secure location outside the repository.
- Use filesystem permissions appropriate for the local OS.

Recommended configuration:

```bash
SHEETSMCP_GOOGLE_APPLICATION_CREDENTIALS=/secure/path/service-account.json
```

## Spreadsheet Access

Access is granted by sharing a spreadsheet with the service-account email. The server cannot list all spreadsheets that were shared with the service account and must not implement Google Drive discovery.

Every spreadsheet operation must include a spreadsheet URL or ID in the MCP tool-call arguments. The MCP server must not accept a configured default spreadsheet ID, because it is a bridge that can operate on any explicitly supplied spreadsheet that the service account can access.

If access fails, return a clear permission or not-found error. Do not attempt to discover alternative files.

## Google API Scope

Use the narrowest Google Sheets scope required by the configured server mode:

- Read-only mode: `https://www.googleapis.com/auth/spreadsheets.readonly`
- Read/write mode: `https://www.googleapis.com/auth/spreadsheets`

Do not request Drive scopes for v1.

## MCP Tool Safety

LLM-driven tools need constrained behavior. SheetsMCP should expose task-specific tools and reject arbitrary Google API passthroughs.

Required write guardrails:

- Validate spreadsheet IDs or URLs.
- Validate A1 ranges.
- Reject unbounded destructive updates.
- Require preview and confirmation for broad batch writes.
- Summarize changes without dumping sensitive spreadsheet contents.
- Keep write operations scoped to the provided spreadsheet.

## Audit Logging

When enabled, audit logs should capture:

- timestamp
- tool name
- spreadsheet ID
- target range or sheet
- write type
- row/column counts
- success or failure

Audit logs should not include:

- service-account key contents
- access tokens
- full cell values by default
- raw request bodies that may contain spreadsheet data

## MCP Client Config

MCP client config may include the executable path and non-secret environment values. Avoid putting secrets directly into shared client config files.

Acceptable:

```json
{
  "command": "/path/to/sheetsmcp",
  "env": {
    "SHEETSMCP_GOOGLE_APPLICATION_CREDENTIALS": "/secure/path/service-account.json"
  }
}
```

Do not include spreadsheet IDs or URLs in the app launch configuration. They belong in individual MCP tool calls.

Not acceptable:

```json
{
  "env": {
    "GOOGLE_PRIVATE_KEY": "-----BEGIN PRIVATE KEY-----..."
  }
}
```

## Out Of Scope

The following are intentionally out of scope:

- OAuth user consent
- desktop browser login
- refresh-token storage
- user account switching
- domain-wide delegation
- Drive listing or search
