#!/usr/bin/env bash

set -euo pipefail

app_name="SheetsMCP"
server_name="sheetsmcp"
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

install_bin_dir="${SHEETSMCP_INSTALL_BIN_DIR:-$HOME/.local/bin}"
application_support_dir="$HOME/Library/Application Support"
oauth_config_dir="$application_support_dir/$app_name"
oauth_config_path="$oauth_config_dir/oauth_client.json"
oauth_token_dir="$oauth_config_dir/google-oauth"
installed_binary="$install_bin_dir/$server_name"
dry_run="${SHEETSMCP_INSTALL_DRY_RUN:-0}"
assume_yes="${SHEETSMCP_INSTALL_YES:-0}"

info() {
    printf '%s\n' "$*"
}

warn() {
    printf 'Warning: %s\n' "$*" >&2
}

die() {
    printf 'Error: %s\n' "$*" >&2
    exit 1
}

is_interactive() {
    [[ -t 0 && -t 1 && "$assume_yes" != "1" ]]
}

confirm() {
    local prompt="$1"
    local default="${2:-n}"
    local suffix
    local answer

    if [[ "$assume_yes" == "1" ]]; then
        [[ "$default" == "y" ]]
        return
    fi

    if ! is_interactive; then
        [[ "$default" == "y" ]]
        return
    fi

    if [[ "$default" == "y" ]]; then
        suffix="[Y/n]"
    else
        suffix="[y/N]"
    fi

    read -r -p "$prompt $suffix " answer
    answer="${answer:-$default}"
    [[ "$answer" =~ ^[Yy]$|^[Yy][Ee][Ss]$ ]]
}

backup_file() {
    local path="$1"
    local backup="$path.sheetsmcp-backup-$(date +%Y%m%d%H%M%S)"
    cp "$path" "$backup"
    info "Backed up $path to $backup"
}

print_mcp_snippets() {
    cat <<EOF

MCP client configuration:

Claude Desktop JSON:
{
  "mcpServers": {
    "sheetsmcp": {
      "command": "$installed_binary",
      "args": []
    }
  }
}

Codex TOML:
[mcp_servers.sheetsmcp]
command = "$installed_binary"
args = []
EOF
}

install_binary() {
    local source_binary="$script_dir/$server_name"
    [[ -f "$source_binary" ]] || die "Could not find packaged binary at $source_binary"

    mkdir -p "$install_bin_dir" "$oauth_config_dir" "$(dirname -- "$oauth_token_dir")"
    install -m 0755 "$source_binary" "$installed_binary"
    info "Installed $server_name to $installed_binary"

    case ":$PATH:" in
        *":$install_bin_dir:"*) ;;
        *) warn "$install_bin_dir is not on PATH. Add it to your shell profile or use $installed_binary directly." ;;
    esac
}

install_bundled_oauth_config() {
    local bundled="$script_dir/oauth_client.json"
    if [[ ! -f "$bundled" ]]; then
        return 1
    fi

    install -m 0600 "$bundled" "$oauth_config_path"
    info "Installed bundled Google OAuth client config to $oauth_config_path"
    return 0
}

prompt_for_oauth_config() {
    local source_path

    if [[ -f "$oauth_config_path" ]]; then
        info "Google OAuth client config already exists at $oauth_config_path"
        return 0
    fi

    if install_bundled_oauth_config; then
        return 0
    fi

    warn "No bundled Google OAuth client config was included in this package."
    info "Browser login needs a Google Cloud OAuth Desktop app client JSON before it can start."

    if ! confirm "Copy a local OAuth client JSON into place now?" "n"; then
        return 1
    fi

    if ! is_interactive; then
        return 1
    fi

    read -r -p "Path to OAuth client JSON: " source_path
    source_path="${source_path/#\~/$HOME}"

    [[ -f "$source_path" ]] || die "OAuth client JSON was not found: $source_path"
    install -m 0600 "$source_path" "$oauth_config_path"
    info "Installed Google OAuth client config to $oauth_config_path"
}

configure_claude() {
    local claude_config="$application_support_dir/Claude/claude_desktop_config.json"

    if [[ ! -f "$claude_config" ]]; then
        info "Claude Desktop config not found at $claude_config"
        return 0
    fi

    if ! confirm "Configure Claude Desktop to launch $server_name?" "y"; then
        return 0
    fi

    if ! command -v python3 >/dev/null 2>&1; then
        warn "python3 was not found; cannot safely edit Claude Desktop JSON."
        print_mcp_snippets
        return 0
    fi

    backup_file "$claude_config"
    python3 - "$claude_config" "$installed_binary" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
command = sys.argv[2]

with path.open("r", encoding="utf-8") as handle:
    data = json.load(handle)

servers = data.setdefault("mcpServers", {})
servers["sheetsmcp"] = {"command": command, "args": []}

with path.open("w", encoding="utf-8") as handle:
    json.dump(data, handle, indent=2)
    handle.write("\n")
PY
    info "Configured Claude Desktop."
}

configure_codex() {
    local codex_config="$HOME/.codex/config.toml"
    local tmp_file

    if [[ ! -f "$codex_config" ]]; then
        info "Codex config not found at $codex_config"
        return 0
    fi

    if ! confirm "Configure Codex to launch $server_name?" "y"; then
        return 0
    fi

    backup_file "$codex_config"
    tmp_file="$(mktemp)"
    awk '
        /^\[mcp_servers\.sheetsmcp(\.|\])/ { skip = 1; next }
        /^\[/ && skip { skip = 0 }
        !skip { print }
    ' "$codex_config" >"$tmp_file"

    {
        printf '\n[mcp_servers.sheetsmcp]\n'
        printf 'command = "%s"\n' "$installed_binary"
        printf 'args = []\n'
    } >>"$tmp_file"

    mv "$tmp_file" "$codex_config"
    info "Configured Codex."
}

run_oauth_login() {
    if [[ ! -f "$oauth_config_path" ]]; then
        warn "Skipping Google login because OAuth client config is missing."
        info "Expected path: $oauth_config_path"
        return 0
    fi

    if [[ "$dry_run" == "1" ]]; then
        info "Dry run: skipping Google OAuth login."
        return 0
    fi

    if confirm "Run Google browser login now?" "y"; then
        "$installed_binary" auth login
    fi

    "$installed_binary" auth status || true
}

main() {
    info "$app_name macOS installer"
    install_binary
    prompt_for_oauth_config || true

    if confirm "Configure supported MCP clients now?" "y"; then
        configure_claude
        configure_codex
    else
        print_mcp_snippets
    fi

    run_oauth_login

    info ""
    info "$app_name install complete."
    info "Executable: $installed_binary"
    info "OAuth client config: $oauth_config_path"
    info "OAuth token store: $oauth_token_dir"
}

main "$@"
