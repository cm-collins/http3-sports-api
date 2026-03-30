#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STATE_DIR="${ROOT_DIR}/scripts/.state"
mkdir -p "${STATE_DIR}"

# Default to Development so `appsettings.Development.json` is used when scripts start the API.
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"

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

try_read_config_string() {
  local file="$1"
  local section="$2"
  local key="$3"

  [[ -f "${file}" ]] || return 1

  # Returns 0 if the key exists in the section (even if the value is empty).
  # Assumes config is the simple shape used in this repo:
  # "Section": { "Key": "Value", ... }
  awk -v section="${section}" -v key="${key}" '
    BEGIN { in_section=0; found=0 }
    $0 ~ "\""section"\"" && $0 ~ "{" { in_section=1; next }
    in_section && $0 ~ "\""key"\"" {
      line=$0
      sub(/.*:/, "", line)
      gsub(/^[[:space:]]*/, "", line)
      gsub(/[[:space:]]*,?[[:space:]]*$/, "", line)
      sub(/^"/, "", line)
      sub(/"$/, "", line)
      print line
      found=1
      exit
    }
    in_section && $0 ~ /^[[:space:]]*}[[:space:]]*,?[[:space:]]*$/ { in_section=0 }
    END { exit(found ? 0 : 1) }
  ' "${file}"
}

config_string() {
  # Reads from appsettings.{ENV}.json first (if present), then appsettings.json.
  # Mirrors .NET behavior where env-specific overrides base.
  local section="$1"
  local key="$2"
  local env="${ASPNETCORE_ENVIRONMENT:-Development}"

  local env_file="${ROOT_DIR}/appsettings.${env}.json"
  local base_file="${ROOT_DIR}/appsettings.json"

  if try_read_config_string "${env_file}" "${section}" "${key}"; then
    return 0
  fi

  try_read_config_string "${base_file}" "${section}" "${key}"
}

warn_if_missing_creds() {
  local api_key
  api_key="$(config_string "ApiFootball" "ApiKey" || true)"
  if [[ -z "${api_key}" ]]; then
    print_section "Config: ApiFootball:ApiKey is empty in appsettings (match endpoints will return 503)"
    echo "Set ApiFootball:ApiKey in appsettings.${ASPNETCORE_ENVIRONMENT}.json (local only), or use user-secrets:"
    echo "  dotnet user-secrets set \"ApiFootball:ApiKey\" \"YOUR_KEY\" --project LiveMatchApi.csproj"
    echo "Note: scripts only read appsettings*.json for this warning; they cannot see user-secrets."
  fi

  local sb_token
  sb_token="$(config_string "ScoreBat" "Token" || true)"
  if [[ -z "${sb_token}" ]]; then
    print_section "Config: ScoreBat:Token is empty in appsettings (highlights may degrade/403 depending on upstream)"
    echo "Set ScoreBat:Token in appsettings.${ASPNETCORE_ENVIRONMENT}.json (local only), or use user-secrets:"
    echo "  dotnet user-secrets set \"ScoreBat:Token\" \"YOUR_TOKEN\" --project LiveMatchApi.csproj"
    echo "Note: scripts only read appsettings*.json for this warning; they cannot see user-secrets."
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
