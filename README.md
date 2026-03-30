# http3-sports-api

> Live sports companion API built to showcase **HTTP/3 (QUIC)** advantages over HTTP/2.

**Backend:** .NET 10 (ASP.NET Core + Kestrel)  
**Mobile:** Kotlin Android *(planned; not in this repo yet)*  
**Protocols:** HTTP/1.1 + HTTP/2 (TCP) and HTTP/3 (QUIC over UDP, TLS 1.3)

---

## Current Status (Repo Today)

Implemented:
- Minimal ASP.NET Core API with API-Football-backed live matches (requires configuration)
- Port `5000` (HTTP): HTTP/1.1 + HTTP/2
- Port `5001` (HTTPS): HTTP/1.1 + HTTP/2 + HTTP/3 (enabled only if a localhost dev cert is found)
- Endpoints: `/`, `/health`, `/api/live-matches`, `/api/live-matches/{id}`

Planned (documented, not implemented yet):
- External API integrations (API-Football, ScoreBat, TheSportsDB)
- SSE match stream endpoints
- Highlights + stats endpoints
- Benchmark endpoints + dashboard
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
│   └── GO-LIVE.md               # Deployment and release plan (planning)
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
- `http://localhost:5000`  (HTTP/1.1 + HTTP/2)
- `https://localhost:5001` (HTTP/1.1 + HTTP/2 + HTTP/3, when a localhost dev cert is available)

### Enable HTTPS + HTTP/3 in Development

HTTP/3 requires TLS. This app enables the HTTPS listener only if it can find a valid `CN=localhost` certificate in the current user cert store.

In the devcontainer, the setup runs:

```bash
dotnet dev-certs https
dotnet dev-certs https --trust || true
```

If no dev cert is found, the app still runs on port `5000` and logs a warning.

### Verify HTTP/3 (Development)

This project does not yet emit `Alt-Svc` headers (that’s planned). To force HTTP/3 during testing, use curl:

```bash
curl -v --http3 https://localhost:5001/health
```

---

## API Endpoints

### Implemented Today

| Method | Endpoint | Description |
|---|---|---|
| GET | `/` | Service info + advertised endpoints |
| GET | `/health` | Health check |
| GET | `/api/live-matches` | List live matches (API-Football; returns 503 if not configured) |
| GET | `/api/live-matches/{id}` | Get one match by id (API-Football; returns 503 if not configured) |

---

## Docs

- `docs/PRD-LiveSportsApp.md` (what we’re building and why)
- `docs/TRD-LiveSportsApp.md` (how we’re building it)
- `docs/GO-LIVE.md` (what “live” means and the rollout checklist)
- `docs/TLS-UDP-QUIC-HTTP3.md` (protocol reference notes)

---

## Roadmap (Implementation)

- [x] Minimal backend skeleton + real live-match data source
- [x] Devcontainer (.NET 10, Ubuntu 24.04)
- [ ] Replace in-memory matches with external API integration + caching
- [ ] Add SSE stream endpoints (scores/commentary)
- [ ] Add highlights + stats endpoints
- [ ] Add benchmark endpoints + results capture
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
