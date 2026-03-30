#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STATE_DIR="${ROOT_DIR}/scripts/.state"
mkdir -p "${STATE_DIR}"

BASE_HTTP_DEFAULT="http://localhost:5000"
BASE_HTTPS_DEFAULT="https://localhost:5001"

use_https() {
  [[ "${USE_HTTPS:-0}" == "1" ]]
}

base_url() {
  if use_https; then
    echo "${BASE_HTTPS:-$BASE_HTTPS_DEFAULT}"
  else
    echo "${BASE_HTTP:-$BASE_HTTP_DEFAULT}"
  fi
}

curl_common_args() {
  if use_https; then
    echo "-k"
  else
    echo ""
  fi
}

print_section() {
  echo
  echo "==> $*"
}

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 127
  fi
}

json_pretty() {
  # Optional: pretty print JSON if `jq` is available.
  if command -v jq >/dev/null 2>&1; then
    jq .
  else
    cat
  fi
}

curl_json() {
  local path="$1"
  local url
  url="$(base_url)${path}"
  print_section "GET ${url}"

  # Print body then status code on the last line for easy scanning.
  local tmp
  tmp="$(mktemp)"
  set +e
  curl -sS $(curl_common_args) -D "${tmp}.headers" -o "${tmp}.body" "${url}"
  local code=$?
  set -e

  if [[ -s "${tmp}.headers" ]]; then
    # Show a small header subset when present.
    grep -Ei '^(HTTP/|content-type:|server-timing:|x-cache:|x-protocol:|x-quic-supported:|x-http3-enabled:|x-payload-kb:)' "${tmp}.headers" || true
  fi

  cat "${tmp}.body" | json_pretty || true

  rm -f "${tmp}.headers" "${tmp}.body"

  # curl exit code isn't the HTTP status. If curl failed, surface it.
  if [[ $code -ne 0 ]]; then
    echo "curl failed (exit=$code)" >&2
    return $code
  fi
}

curl_sse() {
  local path="$1"
  local seconds="${2:-5}"
  local url
  url="$(base_url)${path}"
  print_section "SSE ${url} (showing ~${seconds}s)"
  # `--max-time` ends the stream automatically so scripts don't hang.
  curl -sS $(curl_common_args) -N --max-time "${seconds}" "${url}" || true
}

wait_for_health() {
  local url
  url="$(base_url)/health"
  local deadline
  deadline=$((SECONDS + 20))
  while (( SECONDS < deadline )); do
    if curl -sS $(curl_common_args) "${url}" >/dev/null 2>&1; then
      return 0
    fi
    sleep 0.25
  done
  return 1
}

start_api_if_needed() {
  require_cmd dotnet
  require_cmd curl

  if wait_for_health; then
    print_section "API already running at $(base_url)"
    return 0
  fi

  print_section "Starting API (background)"
  local log_file="${STATE_DIR}/api.log"
  rm -f "${STATE_DIR}/api.pid"

  (
    cd "${ROOT_DIR}"
    # `--no-launch-profile` keeps output stable for scripts and CI.
    dotnet run --project LiveMatchApi.csproj --no-launch-profile
  ) >"${log_file}" 2>&1 &

  local pid=$!
  echo "${pid}" > "${STATE_DIR}/api.pid"

  if ! wait_for_health; then
    echo "API failed to start. Recent logs:" >&2
    tail -n 200 "${log_file}" >&2 || true
    exit 1
  fi

  print_section "API is ready at $(base_url) (pid=${pid})"
}

stop_api_if_started_by_script() {
  if [[ -f "${STATE_DIR}/api.pid" ]]; then
    local pid
    pid="$(cat "${STATE_DIR}/api.pid" || true)"
    if [[ -n "${pid}" ]] && kill -0 "${pid}" >/dev/null 2>&1; then
      print_section "Stopping API (pid=${pid})"
      kill "${pid}" >/dev/null 2>&1 || true
      # Give it a moment to shut down cleanly.
      for _ in {1..20}; do
        if ! kill -0 "${pid}" >/dev/null 2>&1; then
          break
        fi
        sleep 0.1
      done
      if kill -0 "${pid}" >/dev/null 2>&1; then
        kill -9 "${pid}" >/dev/null 2>&1 || true
      fi
    fi
    rm -f "${STATE_DIR}/api.pid"
  fi
}

extract_first_fixture_id() {
  # Reads JSON from stdin and prints the first `matches[].matchId` (or empty).
  # This is intentionally lightweight (no jq/python requirement).
  # Expected API shape includes `"matchId": 123`.
  local id
  id="$(grep -oE '\"matchId\"[[:space:]]*:[[:space:]]*[0-9]+' | head -n 1 | grep -oE '[0-9]+' || true)"
  echo "${id}"
}
