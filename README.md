# http3-sports-api

> Live sports companion API built to showcase **HTTP/3 (QUIC)** advantages over HTTP/2.

**Backend:** .NET 10 (ASP.NET Core + Kestrel)  
**Mobile:** Kotlin Android *(planned; not in this repo yet)*  
**Protocols:** HTTP/1.1 (TCP) and HTTP/2 + HTTP/3 (HTTPS, QUIC over UDP for HTTP/3)

---

## Current Status (Repo Today)

Implemented:
- Minimal ASP.NET Core API with API-Football-backed live matches (requires configuration)
- Port `5000` (HTTP): HTTP/1.1
- Port `5001` (HTTPS): HTTP/1.1 + HTTP/2 + HTTP/3 (enabled only if a localhost dev cert is found and QUIC is supported)
- Endpoints: `/`, `/health`, `/api/matches/live`, `/api/matches/upcoming`, `/api/matches/{fixtureId}` (plus `/api/live-matches` alias)
- Phase 2 endpoints:
  - `/api/match/{fixtureId}/stream` (SSE score updates + goals + match end)
  - `/api/match/{fixtureId}/score-stream` and `/api/match/{fixtureId}/commentary-stream` (optional split streams)
  - `/api/match/{fixtureId}/stats` (API-Football stats; degrades gracefully on upstream errors)
  - `/api/highlights/feed` and `/api/highlights/{team}` (ScoreBat highlights; degrades gracefully)
- Phase 3 endpoints:
  - `/api/benchmark/ping`
  - `/api/benchmark/payload/{kb}`
  - `/api/benchmark/panel/{name}?delayMs=...`
  - `/api/benchmark/stream?intervalMs=...` (SSE)

Planned (documented, not implemented yet):
- External API integrations (TheSportsDB team meta)
- Android benchmark dashboard (client)
- Android client app

---

## Why This Project?

HTTP/3 solves real problems that live sports fans experience every day on mobile:

| Problem | HTTP/2 | HTTP/3 (QUIC) |
|---|---|---|
| One slow stream blocks all others | ❌ Head-of-line blocking | ✅ Independent streams |
| Switching Wi-Fi → 4G drops session | ❌ Reconnects required | ✅ Connection migration |
| High latency on mobile networks | ❌ Multi-RTT handshake | ✅ 0-RTT / 1-RTT |
| Packet loss during live match | ❌ All streams stall | ✅ Only affected stream retransmits |

---

## Architecture (Target)

```
┌──────────────────────┐         QUIC / HTTP3          ┌─────────────────────┐
│   Kotlin Android App │ ◄───────────────────────────► │  .NET 10 Backend    │
│   (future phase)     │      concurrent streams       │  Kestrel HTTP/3     │
└──────────────────────┘                                └─────────────────────┘
                                                                  │
                              ┌───────────────────┬──────────────┤
                              ▼                   ▼              ▼
                        API-Football          ScoreBat     TheSportsDB
                       (Scores/Stats)       (Highlights)    (Team Meta)
```

---

## Project Structure

```
http3-sports-api/
├── Program.cs                   # Kestrel ports + minimal endpoints
├── LiveMatchApi.csproj          # .NET 10 project (net10.0)
├── Models/                      # Domain models (currently: LiveMatch)
├── Services/                    # Repositories (API-backed)
├── docs/
│   ├── PRD-LiveSportsApp.md     # Product Requirements (planning)
│   ├── TRD-LiveSportsApp.md     # Technical Requirements (planning)
│   ├── GO-LIVE.md               # Deployment and release plan (planning)
│   ├── TLS-UDP-QUIC-HTTP3.md     # Protocol reference notes
│   └── plans/                   # Phase-by-phase implementation plan
├── results/
│   └── benchmarks.md            # Benchmark results capture (planning)
└── README.md
```

---

## Getting Started

### Prerequisites

- .NET 10 SDK (this repo targets `net10.0`)
- API-Football key (set as `ApiFootball__ApiKey`)

### Run Locally

```bash
dotnet restore
dotnet run --project LiveMatchApi.csproj
```

Server starts on:
- `http://localhost:5000`  (HTTP/1.1)
- `https://localhost:5001` (HTTP/1.1 + HTTP/2 + HTTP/3, when a localhost dev cert is available and QUIC is supported)

### Enable HTTPS + HTTP/3 in Development

HTTP/3 requires TLS. This app enables the HTTPS listener only if it can find a valid `CN=localhost` certificate in the current user cert store.

In the devcontainer, the setup runs:

```bash
dotnet dev-certs https
dotnet dev-certs https --trust || true
```

If no dev cert is found, the app still runs on port `5000` and logs a warning.

If QUIC is not supported in your environment, the HTTPS listener still works, but HTTP/3 is disabled and only HTTP/1.1 + HTTP/2 will be available on port `5001`.

### Verify HTTP/3 (Development)

This project does not yet emit `Alt-Svc` headers (that’s planned). To force protocol during testing:

HTTP/3 requires QUIC support in the runtime environment. If QUIC is not supported, HTTP/3 will be disabled even if HTTPS is enabled.

If your `curl` build supports HTTP/3:
```bash
curl -v --http3 -k https://localhost:5001/health
```

If your `curl` does not support HTTP/3, use the included probe tool:

```bash
dotnet run --project tools/ProtocolProbe/ProtocolProbe.csproj -- --url https://localhost:5001/health --h2 --insecure
dotnet run --project tools/ProtocolProbe/ProtocolProbe.csproj -- --url https://localhost:5001/health --h3 --insecure
```

---

## Curl Commands (Smoke Tests)

Set base URLs:

```bash
BASE_HTTP=http://localhost:5000
BASE_HTTPS=https://localhost:5001
```

Basic service checks:

```bash
curl -sS $BASE_HTTP/health
curl -sS $BASE_HTTP/
```

Matches (requires `ApiFootball__ApiKey` configured; otherwise returns 503):

```bash
curl -sS $BASE_HTTP/api/matches/live
curl -sS $BASE_HTTP/api/matches/upcoming

# Replace {fixtureId} with a real fixture id from /api/matches/live or /api/matches/upcoming
curl -sS $BASE_HTTP/api/matches/{fixtureId}
```

Match stats:

```bash
curl -sS $BASE_HTTP/api/match/{fixtureId}/stats
```

Match SSE streams (use `-N` to keep the connection open):

```bash
curl -N $BASE_HTTP/api/match/{fixtureId}/stream
curl -N $BASE_HTTP/api/match/{fixtureId}/score-stream
curl -N $BASE_HTTP/api/match/{fixtureId}/commentary-stream
```

Highlights (ScoreBat; returns a degraded response if upstream is unavailable):

```bash
curl -sS $BASE_HTTP/api/highlights/feed
curl -sS $BASE_HTTP/api/highlights/arsenal
```

Benchmarks (no upstream dependencies):

```bash
curl -sS $BASE_HTTP/api/benchmark/ping
curl -sS -D - $BASE_HTTP/api/benchmark/payload/100 -o /dev/null | grep -E "HTTP/|Content-Length|X-Cache|X-Payload-Kb|X-Protocol|X-Quic-Supported|X-Http3-Enabled|Server-Timing"
curl -sS $BASE_HTTP/api/benchmark/panel/slow?delayMs=1500
curl -N $BASE_HTTP/api/benchmark/stream?intervalMs=250
```

Force protocol (when HTTPS is enabled on `:5001`):

```bash
# HTTP/2
curl -sS -k --http2 $BASE_HTTPS/api/benchmark/ping

# HTTP/3 (requires curl built with HTTP/3 support, and QUIC enabled in the runtime env)
curl -sS -k --http3 $BASE_HTTPS/api/benchmark/ping
```

## Automated Tests

This repo includes real automated tests (integration + service parsing/unit tests).

Run:

```bash
dotnet test http3-sports-api.sln
```

## Scripts

There are user-style interaction scripts under `scripts/` (they start the API and call endpoints).
See `scripts/README.md`.

## API Endpoints

### Implemented Today

| Method | Endpoint | Description |
|---|---|---|
| GET | `/` | Service info + advertised endpoints |
| GET | `/health` | Health check |
| GET | `/api/matches/live` | List live matches (API-Football; returns 503 if not configured) |
| GET | `/api/matches/upcoming` | Upcoming fixtures (next 24h, API-Football; returns 503 if not configured) |
| GET | `/api/matches/{fixtureId}` | Get a match by fixture id (API-Football; returns 503 if not configured) |
| GET | `/api/match/{fixtureId}/stream` | SSE stream: score updates + key events (requires API-Football configured) |
| GET | `/api/match/{fixtureId}/score-stream` | SSE stream: score-only (requires API-Football configured) |
| GET | `/api/match/{fixtureId}/commentary-stream` | SSE stream: key events (requires API-Football configured) |
| GET | `/api/match/{fixtureId}/stats` | Match stats (requires API-Football configured; returns degraded response on upstream issues) |
| GET | `/api/highlights/feed` | Recent highlights (ScoreBat; returns degraded response on upstream issues) |
| GET | `/api/highlights/{team}` | Team highlights (ScoreBat; returns degraded response on upstream issues) |
| GET | `/api/live-matches` | Alias of `/api/matches/live` |

---

## Docs

- `docs/PRD-LiveSportsApp.md` (what we’re building and why)
- `docs/TRD-LiveSportsApp.md` (how we’re building it)
- `docs/GO-LIVE.md` (what “live” means and the rollout checklist)
- `docs/plans/README.md` (phase-by-phase delivery plan)
- `docs/TLS-UDP-QUIC-HTTP3.md` (protocol reference notes)

---

## Roadmap (Implementation)

- [x] Minimal backend skeleton + real live-match data source
- [x] Devcontainer (.NET 10, Ubuntu 24.04)
- [x] API-Football integration + caching (live + upcoming)
- [x] Add SSE stream endpoints (score + key events)
- [x] Add highlights + stats endpoints
- [x] Add benchmark endpoints (ping/payload/panels/stream)
- [ ] Capture benchmark results (see `results/benchmarks.md`)
- [ ] Deploy staging + production (see `docs/GO-LIVE.md`)
- [ ] Kotlin Android app (separate repo/folder)

---

## External APIs (Planned)

| API | Purpose | Notes |
|---|---|---|
| API-Football (api-sports.io) | Live scores, stats, fixtures | API key required |
| ScoreBat Video API | Goal highlight embed URLs | Basic feed unauthenticated |
| TheSportsDB | Team logos + metadata | Free tiers vary |

---

## License

MIT
