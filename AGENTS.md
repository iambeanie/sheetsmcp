# AGENTS.md

Guidance for coding agents and contributors working in this repository.

## Project Intent

SheetsMCP is a local Model Context Protocol server for reading and writing Google Sheets. It is built in C# on .NET 10, runs locally over MCP stdio, and is distributed as self-contained executables for Linux, macOS, and Windows.

The authentication model is permanently user OAuth for a local desktop/stdio app. Do not add service-account auth, domain-wide delegation, Drive file discovery, or MCP server OAuth unless the project direction is explicitly changed by the owner.

## Non-Negotiable Constraints

- Target .NET 10.
- Use the official C# MCP SDK package `ModelContextProtocol` for stdio MCP support.
- Use official Google .NET libraries: `Google.Apis.Auth` and `Google.Apis.Sheets.v4`.
- Use Google user OAuth installed-app flow only.
- Require every spreadsheet operation to receive a spreadsheet URL or spreadsheet ID in the MCP tool call.
- Do not configure, store, infer, or default the active spreadsheet at MCP server startup.
- Do not list, search, discover, or enumerate Google Drive files.
- Do not expose a raw Google API execution tool.
- Ship runnable artifacts as self-contained platform-specific binaries.
- Never commit OAuth client JSON, token caches, secrets, generated credentials, logs with secrets, or local MCP client config containing secrets.

## Expected Architecture

Keep the implementation split into small layers:

- MCP tool layer: validates tool inputs, shapes responses, and enforces write guardrails.
- Sheets service layer: wraps `Google.Apis.Sheets.v4` calls behind project-owned methods.
- Auth/config layer: loads OAuth client configuration, cached user tokens, and app configuration.
- Range and spreadsheet parsing helpers: normalize spreadsheet IDs, URLs, ranges, and sheet names.

The MCP transport is stdio.

## Tool Design Rules

Prefer narrow, predictable MCP tools:

- `get_spreadsheet_metadata`
- `read_range`
- `find_values`
- `append_rows`
- `update_range`
- `batch_update_with_preview`
- `confirm_batch_update`

Write tools must be conservative:

- Validate spreadsheet IDs, links, sheet names, and A1 ranges before calling Google.
- Prefer preview/dry-run behavior for broad or destructive changes.
- Require an explicit confirmation token or operation ID before applying destructive batch updates.
- Return concise summaries of changed ranges and row counts.
- Log write attempts without logging cell contents that may be sensitive.

Avoid generic tools such as `execute_raw_google_api_request`, arbitrary JSON passthroughs, or tools that let the model construct unbounded Google API calls.

## Configuration Rules

Configuration should be cross-platform and explicit. Use fixed per-user defaults for OAuth files and environment variables only for non-secret runtime options.

Recommended names:

- `SHEETSMCP_AUDIT_LOG_PATH`: optional path for local write audit logs.

Do not require users to place secrets in MCP client config. MCP client config should launch the executable and pass only non-secret arguments where possible. Spreadsheet IDs and URLs belong in MCP tool-call arguments, not in the server configuration or MCP client launch config.

## Packaging Rules

The primary release artifacts are self-contained .NET 10 binaries built per runtime identifier:

- `linux-x64`
- `linux-arm64`
- `osx-x64`
- `osx-arm64`
- `win-x64`

Use `dotnet publish` with self-contained output. Single-file publishing is expected unless a dependency proves incompatible and the reason is documented.

## Validation Expectations

Before marking implementation work complete:

- Run `./scripts/dotnet-verify-all`.
- Validate at least one read-only Sheets call against a test spreadsheet when credentials are available.
- Validate write tools against a disposable test spreadsheet when credentials are available.
- Confirm published binaries start without requiring a system .NET runtime.

If credentials are not available, state which Google integration checks could not be run.

## Agent Workflow Scripts

Agents should use the scripts in `scripts/` instead of invoking raw .NET build commands. The scripts force single-node MSBuild where supported and suppress routine output. Clean operations print one `OK` line; warnings and failures replay the captured command output.

- `./scripts/dotnet-restore`
- `./scripts/dotnet-build`
- `./scripts/dotnet-test`
- `./scripts/dotnet-format-check`
- `./scripts/dotnet-verify-all`
- `./scripts/dotnet-integration-test`
- `./scripts/dotnet-publish [all|RID]`
- `./scripts/dotnet-release`

`dotnet-integration-test` reports missing credentials or spreadsheet configuration as skipped. Set `SHEETSMCP_INTEGRATION_WRITE=1` only when the configured spreadsheet is disposable and write validation is intended.
