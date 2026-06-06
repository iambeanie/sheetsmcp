# Install SheetsMCP on Linux x64

Extract the package and run:

```bash
./install.sh
```

The installer copies the self-contained `sheetsmcp` binary to `~/.local/bin/sheetsmcp`, configures per-user OAuth folders, can add MCP client config entries for Claude Desktop and Codex, and offers to run `sheetsmcp auth login`.

Some desktop environments allow double-clicking `Install SheetsMCP.desktop`. If that does not open a terminal installer, use `./install.sh`.

Google browser login requires an OAuth Desktop app client config. Release packages can include the SheetsMCP app-owned OAuth client config; otherwise the installer will ask for a local `oauth_client.json` downloaded from Google Cloud.
