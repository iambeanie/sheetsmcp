# SheetsMCP

SheetsMCP is a local MCP server for reading and writing Google Sheets from MCP-capable clients. It uses Google user OAuth so local tools can access spreadsheets available to the signed-in Google user.

The app runs locally over stdio and is intended to ship as standalone executables for Linux, macOS, and Windows. Target machines should not need to install .NET or any other runtime.

## Current Direction

- Language/runtime: C# on .NET 10.
- MCP transport: local stdio.
- Google auth: installed-app user OAuth.
- Google APIs: Google Sheets API.
- Spreadsheet access: by explicit spreadsheet URL or spreadsheet ID only.
- Runtime model: bridge process; the active spreadsheet is supplied per MCP tool call, not configured at startup.
- Distribution: self-contained platform-specific binaries.

SheetsMCP does not support service-account auth, domain-wide delegation, Drive search, or listing available spreadsheets.

## Access Model

SheetsMCP acts as the signed-in Google user. To use a spreadsheet:

1. Create a Google Cloud OAuth client with application type `Desktop app`.
2. Enable the Google Sheets API in that Google Cloud project.
3. Place the downloaded OAuth client JSON at the default local config path.
4. Run `sheetsmcp auth login` and complete the browser consent flow.
5. Call MCP tools with the spreadsheet URL or spreadsheet ID.

The app cannot discover which spreadsheets the signed-in user can access. If the user cannot access a spreadsheet, Google Sheets API calls should fail with a permission error.

SheetsMCP is not configured for one worksheet or spreadsheet when the MCP server starts. The server only uses cached OAuth tokens and bridge configuration. Each query that uses a SheetsMCP tool must pass the target spreadsheet URL or ID as part of that tool call.

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

Optional environment variables:

```bash
SHEETSMCP_AUDIT_LOG_PATH=/secure/path/sheetsmcp-audit.log
```

SheetsMCP always uses per-user OAuth paths:

- Linux: `~/.config/sheetsmcp/oauth_client.json` and `~/.local/share/sheetsmcp/google-oauth`
- macOS: `~/Library/Application Support/SheetsMCP/oauth_client.json` and `~/Library/Application Support/SheetsMCP/google-oauth`
- Windows: `%APPDATA%\SheetsMCP\oauth_client.json` and `%APPDATA%\SheetsMCP\google-oauth`

## Linux x64 Install

The Linux x64 release package includes a guided installer:

```bash
mkdir sheetsmcp-linux-x64
tar -xzf sheetsmcp-linux-x64.tar.gz -C sheetsmcp-linux-x64
cd sheetsmcp-linux-x64
./install.sh
```

The installer copies the self-contained executable to `~/.local/bin/sheetsmcp`, creates the per-user OAuth directories, can configure Claude Desktop and Codex MCP client entries, and offers to run `sheetsmcp auth login`.

Some desktop environments also allow double-clicking `Install SheetsMCP.desktop` after extraction. If double-click launch is blocked by the file manager, run `./install.sh` in a terminal.

Run these commands outside the MCP client:

```bash
sheetsmcp auth login
sheetsmcp auth status
sheetsmcp auth logout --yes
```

Do not commit OAuth client JSON files, token caches, or put token values in MCP client config. Do not put spreadsheet IDs in the MCP app configuration; pass the spreadsheet URL or ID with each MCP tool call.

## Example MCP Client Configuration

The exact shape depends on the MCP client. The important part is that the client launches the local executable over stdio and does not contain OAuth token values.

```json
{
  "mcpServers": {
    "sheetsmcp": {
      "command": "/path/to/sheetsmcp",
      "args": []
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

Release owners can include a project-owned Google OAuth Desktop app client config in the generated Linux x64 package without committing it:

```bash
SHEETSMCP_RELEASE_OAUTH_CLIENT_JSON=/private/path/oauth_client.json \
./scripts/dotnet-publish linux-x64
```

That file is copied into the generated package so the installer can start the normal browser OAuth consent flow. If it is not provided, users can still install the binary, but they must supply their own Google OAuth Desktop app client JSON before login can complete.

## Google Integration Validation

Integration checks are separate from deterministic unit tests:

```bash
SHEETSMCP_INTEGRATION_SPREADSHEET=spreadsheet-id \
./scripts/dotnet-integration-test
```

Set `SHEETSMCP_INTEGRATION_WRITE=1` to also append a validation row. Missing integration configuration is reported as skipped.

## Documentation

- [Architecture](docs/architecture.md)
- [Security](docs/security.md)
- [Packaging](docs/packaging.md)
