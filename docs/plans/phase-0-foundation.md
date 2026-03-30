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
  - `5000/tcp`: HTTP/1.1
  - `5001/tcp+udp`: HTTPS with HTTP/1.1, HTTP/2, HTTP/3 when a localhost dev cert is available and QUIC is supported

Data:

- API-Football-backed live match repository:
  - options in `Services/ApiFootballOptions.cs`
  - repository in `Services/ApiFootballLiveMatchRepository.cs`
  - `IMemoryCache` caching for live fixtures

Contracts:

- Response DTOs in `Contracts/` (API returns stable envelopes, not raw domain models)

Endpoints:

- `GET /` service info
- `GET /health`
- `GET /api/matches/live`
- `GET /api/matches/upcoming`
- `GET /api/matches/{fixtureId}`
- `GET /api/live-matches` (alias)

Behavior:

- If `ApiFootball__ApiKey` is not configured, `/api/matches/*` (and the `/api/live-matches` alias) returns HTTP 503 with `ProblemDetails`.
- Match identifiers are provider fixture ids (long).

---

## Configuration

Required for real data:

- `ApiFootball__ApiKey` (environment variable)

Optional tuning:

- `ApiFootball__BaseUrl` (defaults to `https://v3.football.api-sports.io/`)
- `ApiFootball__LiveCacheSeconds` (defaults to `10`)
- `ApiFootball__TimeoutSeconds` (defaults to `10`)

---

## Acceptance Criteria

- `dotnet build http3-sports-api.sln` succeeds.
- Without API key:
  - `GET /api/matches/live` returns 503 with a clear message.
- With API key:
  - `GET /api/matches/live` returns 200 with a response envelope containing `matches` and `meta`.
- `/health` is reachable over HTTP/2 and, when HTTPS is enabled and QUIC is supported, can be forced over HTTP/3.

Protocol verification:

- If your `curl` supports HTTP/3, you can force it directly.
- Otherwise use `tools/ProtocolProbe` to request HTTP/2 and HTTP/3 explicitly.

---

## Clean Code Notes

Before adding features, follow:

- `docs/plans/engineering-guidelines.md`

Phase 0 clean-code baseline includes:

- Response DTOs (no long-term exposure of provider shapes, and no direct exposure of `Models/LiveMatch` as the public contract).
- Consistent `ProblemDetails` responses for missing provider configuration, upstream failures, and not-found.
