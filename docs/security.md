# Security

SheetsMCP uses Google installed-app OAuth to access spreadsheets as the signed-in Google user.

## OAuth Credentials

OAuth client JSON files and cached tokens are sensitive. The client JSON identifies the Google Cloud OAuth app; the token cache lets SheetsMCP refresh access for the signed-in user.

Rules:

- Do not commit OAuth client JSON files or token cache directories.
- Do not paste OAuth token values into MCP client config.
- Do not log OAuth client secrets, access tokens, refresh tokens, or raw Google auth responses.
- Store OAuth files in user-controlled local config/data directories with appropriate filesystem permissions.

The app uses per-user config/data defaults documented in the README.

## Spreadsheet Access

Access is granted by the signed-in Google user's existing Sheets permissions. The server cannot list all spreadsheets the user can access and must not implement Google Drive discovery.

Every spreadsheet operation must include a spreadsheet URL or ID in the MCP tool-call arguments. The MCP server must not accept a configured default spreadsheet ID, because it is a bridge that can operate on any explicitly supplied spreadsheet that the signed-in user can access.

If access fails, return a clear permission or not-found error. Do not attempt to discover alternative files.

## Google API Scope

Use the Google Sheets scope required by the configured server tools:

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

- OAuth client secrets or token values
- full cell values by default
- raw request bodies that may contain spreadsheet data

## MCP Client Config

MCP client config may include the executable path and non-secret environment values. Avoid putting secrets directly into shared client config files.

Acceptable:

```json
{
  "command": "/path/to/sheetsmcp"
}
```

Do not include spreadsheet IDs or URLs in the app launch configuration. They belong in individual MCP tool calls.

Not acceptable:

```json
{
  "env": {
    "GOOGLE_REFRESH_TOKEN": "1//..."
  }
}
```

## Out Of Scope

The following are intentionally out of scope:

- service-account auth
- domain-wide delegation
- Drive listing or search
- MCP server OAuth
