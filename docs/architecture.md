# Architecture

SheetsMCP is a local stdio MCP server that exposes safe Google Sheets operations to MCP clients. It runs as a normal local process launched by the client.

```text
MCP client
   |
   | stdio MCP
   v
SheetsMCP .NET 10 executable
   |
   | Google Sheets API
   v
Google Sheets shared with service account
```

## Runtime Model

The v1 server is a .NET 10 console application using `ModelContextProtocol`. It should use `Microsoft.Extensions.Hosting` for configuration, logging, dependency injection, and clean startup/shutdown behavior.

The process reads and writes MCP messages over stdio. It must not write normal logs to stdout because stdout belongs to the MCP protocol. Human-readable logs should go to stderr or to an explicitly configured audit log path.

SheetsMCP runs as a bridge, not as a process bound to one spreadsheet. Startup configuration establishes credentials, logging, and guardrails only. The target spreadsheet is selected by the MCP tool-call arguments for each query.

## Main Components

### MCP Tool Layer

The tool layer defines the public MCP surface. It should:

- Validate required inputs before calling service code.
- Normalize spreadsheet URLs into spreadsheet IDs.
- Reject missing or ambiguous ranges.
- Enforce write guardrails.
- Return concise structured responses.

The tool layer should not contain direct Google API request construction beyond simple request mapping.

### Sheets Service Layer

The Sheets service layer wraps `Google.Apis.Sheets.v4`. It should provide project-owned methods for operations such as metadata lookup, range reads, appends, updates, and batch updates.

This layer owns Google API error handling and should translate common failures into clear project errors:

- spreadsheet not found
- permission denied
- invalid range
- quota or transient Google API failure
- malformed spreadsheet ID or URL

### Auth And Configuration Layer

The auth layer loads service-account credentials from an explicit path, normally `SHEETSMCP_GOOGLE_APPLICATION_CREDENTIALS`. The credential should be scoped to the Google Sheets API.

The auth/config layer must not load or expose a default spreadsheet ID. The project should avoid broad Google APIs unless the implementation has a direct need. Drive listing and Drive search are out of scope.

### Parsing Helpers

Helpers should handle:

- spreadsheet URLs
- spreadsheet IDs
- A1 ranges
- sheet names that require quoting
- row and column value normalization

Parsing should be deterministic and covered by unit tests.

## Google Access Rules

The server acts only as the service account. Users must share each target spreadsheet with the service-account email address.

The server does not know which spreadsheets it can access. It only works when the caller provides a spreadsheet URL or ID in the MCP tool call. There is no startup spreadsheet, default spreadsheet, Drive search, Drive picker, file listing, or account-wide discovery.

## Tool Surface

Initial tools should be narrow:

- `get_spreadsheet_metadata(spreadsheet)`
- `read_range(spreadsheet, range)`
- `find_values(spreadsheet, ranges, query)`
- `append_rows(spreadsheet, rangeOrSheet, rows)`
- `update_range(spreadsheet, range, values)`
- `batch_update_with_preview(spreadsheet, operations)`
- `confirm_batch_update(operationId)`

`spreadsheet` may be a URL or ID. Internally the app should normalize it before calling Google.

## Write Flow

Simple bounded writes can execute directly after validation. Broad writes should be two-step:

1. Preview the operation and return a summary plus operation ID.
2. Apply the operation only through a confirmation call using that operation ID.

Preview state should be local and short-lived. It must not store service-account secrets or unnecessary cell contents.

## Project Boundaries

The project may add more Sheets-specific tools, but the Google auth model remains service-account-only. Do not design abstractions around user OAuth or account switching.
