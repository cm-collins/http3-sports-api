#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_common.sh
source "${SCRIPT_DIR}/_common.sh"

trap 'stop_api_if_started_by_script' EXIT
start_api_if_needed
warn_if_missing_creds

curl_json "/api/benchmark/ping"

print_section "Benchmark: payload headers"
curl -sS $(curl_common_args) -D - "$(base_url)/api/benchmark/payload/100" -o /dev/null | grep -E "HTTP/|Content-Length|X-Cache|X-Payload-Kb|X-Protocol|X-Quic-Supported|X-Http3-Enabled|Server-Timing" || true

curl_json "/api/benchmark/panel/fast?delayMs=0"
curl_json "/api/benchmark/panel/slow?delayMs=1500"

curl_sse "/api/benchmark/stream?intervalMs=250" "${BENCH_STREAM_SECONDS:-3}"
