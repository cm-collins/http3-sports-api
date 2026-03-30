#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_common.sh
source "${SCRIPT_DIR}/_common.sh"

trap 'stop_api_if_started_by_script' EXIT
start_api_if_needed
warn_if_missing_creds

fixture_id="${1:-${FIXTURE_ID:-}}"
if [[ -z "${fixture_id}" ]]; then
  print_section "Finding a fixtureId from /api/matches/live"
  live_body="$(curl -sS $(curl_common_args) "$(base_url)/api/matches/live" || true)"
  fixture_id="$(echo "${live_body}" | extract_first_fixture_id || true)"
fi

if [[ -z "${fixture_id}" ]]; then
  echo "No fixture id available. Provide one:" >&2
  echo "  scripts/match-stats.sh 123456" >&2
  echo "Or set FIXTURE_ID=..." >&2
  exit 2
fi

curl_json "/api/match/${fixture_id}/stats"
