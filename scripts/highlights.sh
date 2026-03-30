#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_common.sh
source "${SCRIPT_DIR}/_common.sh"

trap 'stop_api_if_started_by_script' EXIT
start_api_if_needed
warn_if_missing_creds

team="${1:-${TEAM:-arsenal}}"

curl_json "/api/highlights/feed"
curl_json "/api/highlights/${team}"
