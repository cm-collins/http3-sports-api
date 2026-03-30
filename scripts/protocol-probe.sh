#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_common.sh
source "${SCRIPT_DIR}/_common.sh"

require_cmd dotnet

url="${1:-https://localhost:5001/api/benchmark/ping}"

print_section "ProtocolProbe (HTTP/2)"
dotnet run --project "${ROOT_DIR}/tools/ProtocolProbe/ProtocolProbe.csproj" -- --url "${url}" --h2 --insecure

print_section "ProtocolProbe (HTTP/3)"
dotnet run --project "${ROOT_DIR}/tools/ProtocolProbe/ProtocolProbe.csproj" -- --url "${url}" --h3 --insecure || true

