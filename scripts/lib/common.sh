#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"
SOLUTION="$REPO_ROOT/SheetsMcp.slnx"
APP_PROJECT="$REPO_ROOT/src/SheetsMcp/SheetsMcp.csproj"
TEST_PROJECT="$REPO_ROOT/tests/SheetsMcp.Tests/SheetsMcp.Tests.csproj"
ARTIFACTS_DIR="$REPO_ROOT/artifacts"

cd "$REPO_ROOT"

run_quiet() {
    local label="$1"
    shift

    local log_file
    log_file="$(mktemp)"

    local exit_code=0
    "$@" >"$log_file" 2>&1 || exit_code=$?

    if (( exit_code != 0 )); then
        printf 'FAIL %s (exit %d)\n' "$label" "$exit_code" >&2
        cat "$log_file" >&2
        rm -f "$log_file"
        return "$exit_code"
    fi

    if grep -Eiv '0 Warning\(s\)' "$log_file" | grep -Eiq '(^|[^[:alnum:]])warning([[:space:]:]|[A-Z]{2,}[0-9])'; then
        printf 'WARN %s\n' "$label"
        cat "$log_file"
    else
        printf 'OK %s\n' "$label"
    fi

    rm -f "$log_file"
}

expand_home() {
    case "$1" in
        "~")
            printf '%s\n' "$HOME"
            ;;
        "~/"*)
            printf '%s/%s\n' "$HOME" "${1#~/}"
            ;;
        *)
            printf '%s\n' "$1"
            ;;
    esac
}
