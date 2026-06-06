# SheetsMCP

SheetsMCP is a local MCP server for reading and writing Google Sheets from MCP-capable clients. It is designed for controlled spreadsheets that are explicitly shared with a Google service account.

The app runs locally over stdio and is intended to ship as standalone executables for Linux, macOS, and Windows. Target machines should not need to install .NET or any other runtime.

## Current Direction

- Language/runtime: C# on .NET 10.
- MCP transport: local stdio.
- Google auth: service account only.
- Google APIs: Google Sheets API.
- Spreadsheet access: by explicit spreadsheet URL or spreadsheet ID only.
- Runtime model: bridge process; the active spreadsheet is supplied per MCP tool call, not configured at startup.
- Distribution: self-contained platform-specific binaries.

SheetsMCP will not support user OAuth, Google account sign-in, refresh-token storage, Drive search, or listing available spreadsheets.

## Access Model

SheetsMCP acts as the service account. To use a spreadsheet:

1. Create or choose a Google Cloud service account.
2. Enable the Google Sheets API in the Google Cloud project.
3. Create a service-account JSON key and store it securely on the local machine.
4. Share the target spreadsheet with the service-account email address.
5. Call MCP tools with the spreadsheet URL or spreadsheet ID.

The app cannot discover which spreadsheets the service account can access. If a spreadsheet was not shared with the service-account email, Google Sheets API calls should fail with a permission error.

SheetsMCP is not configured for one worksheet or spreadsheet when the MCP server starts. The server only holds service-account credentials and bridge configuration. Each query that uses a SheetsMCP tool must pass the target spreadsheet URL or ID as part of that tool call.

## Planned MCP Tools

The initial server should expose narrow tools instead of a generic Google API passthrough:

- `get_spreadsheet_metadata`: return spreadsheet title, sheets, grid sizes, and basic properties.
- `read_range`: read values from an A1 range.
- `find_values`: search provided ranges for matching text or values.
- `append_rows`: append rows to a specific sheet or range.
- `update_range`: update a bounded A1 range.
- `batch_update_with_preview`: prepare a broad write operation and return a preview.
- `confirm_batch_update`: apply a previously previewed batch update.

Write tools should validate ranges and summarize changes. Broad or destructive writes should use a preview/confirm flow.

## Local Development

Expected SDK:

```bash
dotnet --version
```

The project targets .NET 10. Use the repository scripts for concise, agent-friendly output:

```bash
./scripts/dotnet-build
./scripts/dotnet-test
./scripts/dotnet-format-check
./scripts/dotnet-verify-all
dotnet run --project src/SheetsMcp
```

The scripts capture routine .NET output. A clean operation prints one `OK` line; warnings and failures replay the full captured output.

## Configuration

Recommended environment variables:

```bash
SHEETSMCP_GOOGLE_APPLICATION_CREDENTIALS=/secure/path/service-account.json
SHEETSMCP_WRITE_GUARDRAILS=preview-required
SHEETSMCP_AUDIT_LOG_PATH=/secure/path/sheetsmcp-audit.log
```

Do not commit service-account JSON keys or put their contents in MCP client config. Do not put spreadsheet IDs in the MCP app configuration; pass the spreadsheet URL or ID with each MCP tool call.

## Example MCP Client Configuration

The exact shape depends on the MCP client. The important part is that the client launches the local executable over stdio and does not contain the service-account JSON secret.

```json
{
  "mcpServers": {
    "sheetsmcp": {
      "command": "/path/to/sheetsmcp",
      "args": [],
      "env": {
        "SHEETSMCP_GOOGLE_APPLICATION_CREDENTIALS": "/secure/path/service-account.json"
      }
    }
  }
}
```

For Windows, use the published `.exe` path.

The target spreadsheet is intentionally absent from this launch configuration. It is supplied later by the MCP client when a tool such as `read_range` or `update_range` is called.

## Publishing

Publish one runtime or all supported runtimes:

```bash
./scripts/dotnet-publish linux-x64
./scripts/dotnet-publish all
./scripts/dotnet-release
```

Packages, per-RID publish directories, and `SHA256SUMS` are written under `artifacts/`.

## Google Integration Validation

Integration checks are separate from deterministic unit tests:

```bash
SHEETSMCP_GOOGLE_APPLICATION_CREDENTIALS=/secure/path/service-account.json \
SHEETSMCP_INTEGRATION_SPREADSHEET=spreadsheet-id \
./scripts/dotnet-integration-test
```

Set `SHEETSMCP_INTEGRATION_WRITE=1` to also append a validation row. Missing integration configuration is reported as skipped.

## Documentation

- [Architecture](docs/architecture.md)
- [Security](docs/security.md)
- [Packaging](docs/packaging.md)
