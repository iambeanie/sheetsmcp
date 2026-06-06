# Install SheetsMCP on macOS

Extract the package and run:

```bash
./install.sh
```

The installer copies the self-contained `sheetsmcp` binary to `~/.local/bin/sheetsmcp`, configures the per-user OAuth folder at `~/Library/Application Support/SheetsMCP`, can add MCP client config entries for Claude Desktop and Codex, and offers to run `sheetsmcp auth login`.

Google browser login requires an OAuth Desktop app client config. Release packages can include the SheetsMCP app-owned OAuth client config; otherwise the installer will ask for a local `oauth_client.json` downloaded from Google Cloud.

macOS may block unsigned downloaded binaries the first time they run. Signing and notarization are not part of this installer flow yet.
