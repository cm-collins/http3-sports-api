#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_common.sh
source "${SCRIPT_DIR}/_common.sh"

trap 'stop_api_if_started_by_script' EXIT
start_api_if_needed

curl_json "/"
curl_json "/health"

print_section "Matches: live"
live_body="$(curl -sS $(curl_common_args) "$(base_url)/api/matches/live" || true)"
echo "${live_body}" | json_pretty

fixture_id="${FIXTURE_ID:-}"
if [[ -z "${fixture_id}" ]]; then
  fixture_id="$(echo "${live_body}" | extract_first_fixture_id || true)"
fi

if [[ -n "${fixture_id}" ]]; then
  print_section "Matches: by fixture id (${fixture_id})"
  curl_json "/api/matches/${fixture_id}"
else
  echo "No fixture id found. If API-Football isn't configured, set:" >&2
  echo "  export ApiFootball__ApiKey=YOUR_KEY" >&2
  echo "Or provide one explicitly:" >&2
  echo "  FIXTURE_ID=123456 scripts/matches.sh" >&2
fi

print_section "Matches: upcoming"
curl_json "/api/matches/upcoming"

