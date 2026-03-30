# Benchmark Results — HTTP/2 vs HTTP/3

> Results will be recorded here as testing progresses.
>
> Status: `/api/benchmark/*` endpoints are implemented. Use them consistently for protocol comparisons.

## Test Environment

| | Details |
|---|---|
| Device | — |
| Android version | — |
| Network (Wi-Fi) | — |
| Network (Mobile) | — |
| Server location | — |
| Date | — |

### How To Run (Current Repo State)

1. Start the API from repo root:
   - `dotnet run --project LiveMatchApi.csproj`
2. Use the benchmark endpoints:
   - Ping: `GET /api/benchmark/ping`
   - Payload: `GET /api/benchmark/payload/{kb}`
   - SSE stream: `GET /api/benchmark/stream`
3. Force protocol from the client:
   - HTTP/2: `curl -sS -k -o /dev/null -w "%{time_total}\n" --http2 https://localhost:5001/api/benchmark/ping`
   - HTTP/3: use a curl build with HTTP/3 support, or use `tools/ProtocolProbe` (see below)

If your `curl` supports HTTP/3:

- HTTP/3: `curl -sS -k -o /dev/null -w "%{time_total}\n" --http3 https://localhost:5001/api/benchmark/ping`

If your `curl` does not support HTTP/3, use the included probe tool:

- HTTP/2: `dotnet run --project tools/ProtocolProbe/ProtocolProbe.csproj -- --url https://localhost:5001/api/benchmark/ping --h2 --insecure`
- HTTP/3: `dotnet run --project tools/ProtocolProbe/ProtocolProbe.csproj -- --url https://localhost:5001/api/benchmark/ping --h3 --insecure`

Notes:
- HTTP/3 requires TLS and UDP reachability. In a devcontainer, UDP is testable inside the container; VS Code port forwarding is TCP-only.
- For payload tests: `GET /api/benchmark/payload/100`, `.../500`, `.../1024`.

---

## Ping Latency (x10 requests)

| Run | HTTP/2 (ms) | HTTP/3 (ms) |
|---|---|---|
| 1 | — | — |
| 2 | — | — |
| 3 | — | — |
| **Average** | — | — |

---

## Payload Transfer

| Payload Size | HTTP/2 (ms) | HTTP/3 (ms) | Winner |
|---|---|---|---|
| 100 KB | — | — | — |
| 500 KB | — | — | — |
| 1 MB | — | — | — |

---

## 4 Concurrent Streams

| Metric | HTTP/2 | HTTP/3 |
|---|---|---|
| All 4 streams loaded (ms) | — | — |
| Slowest stream (ms) | — | — |
| Stream blocking observed? | — | — |

---

## Network Migration (Wi-Fi → 4G)

| Metric | HTTP/2 | HTTP/3 |
|---|---|---|
| Session survived? | — | — |
| Recovery time (ms) | — | — |
| Streams interrupted | — | — |

---

## Packet Loss Simulation

| Scenario | HTTP/2 | HTTP/3 |
|---|---|---|
| 5% packet loss — all streams stall? | — | — |
| 10% packet loss — recovery time | — | — |

---

## Notes

*Add observations here as testing runs.*
