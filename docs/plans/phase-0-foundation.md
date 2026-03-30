# Phase 0: Foundation (Current Baseline)

**Last Updated:** 2026-03-30  
**Status:** Implemented baseline, ready to extend

This phase represents what exists today in the repository and what we consider the minimum foundation for continued work.

---

## Goals

- Run a .NET 10 backend locally with HTTP/2 and optional HTTP/3.
- Provide a real upstream live-match data source when configured.
- Keep the surface area small while we lock in patterns for clean code.

---

## What Exists Today

Backend:

- .NET 10 minimal API in `Program.cs`
- Kestrel listeners:
  - `5000/tcp`: HTTP/1.1 and HTTP/2
  - `5001/tcp+udp`: HTTPS with HTTP/1.1, HTTP/2, HTTP/3 when a localhost dev cert is available

Data:

- API-Football-backed live match repository:
  - options in `Services/ApiFootballOptions.cs`
  - repository in `Services/ApiFootballLiveMatchRepository.cs`
  - `IMemoryCache` caching for live fixtures

Endpoints:

- `GET /` service info
- `GET /health`
- `GET /api/live-matches`
- `GET /api/live-matches/{id}`

Behavior:

- If `ApiFootball__ApiKey` is not configured, `/api/live-matches*` returns HTTP 503 with `ProblemDetails`.
- Current match ids are GUIDs derived deterministically from upstream fixture ids; Phase 1 will standardize the public contract id format.

---

## Configuration

Required for real data:

- `ApiFootball__ApiKey` (environment variable)

Optional tuning:

- `ApiFootball__BaseUrl` (defaults to `https://v3.football.api-sports.io/`)
- `ApiFootball__LiveCacheSeconds` (defaults to `10`)

---

## Acceptance Criteria

- `dotnet build http3-sports-api.sln` succeeds.
- Without API key:
  - `GET /api/live-matches` returns 503 with a clear message.
- With API key:
  - `GET /api/live-matches` returns 200 and a non-empty array when fixtures are live upstream.
- `/health` is reachable over HTTP/2 and, when HTTPS is enabled, can be forced over HTTP/3.

---

## Clean Code Notes

Before adding features, follow:

- `docs/plans/engineering-guidelines.md`

Immediate follow-ups to keep Phase 0 maintainable:

- Introduce response DTOs (do not expose `Models/LiveMatch` as the long-term public contract).
- Add structured error handling and a consistent `ProblemDetails` strategy across all endpoints.
