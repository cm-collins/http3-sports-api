# Phase 1: Matches and Fixtures (API Contract + Caching)

**Last Updated:** 2026-03-30  
**Status:** Planned

This phase completes the core backend MVP for match discovery and stabilizes the API contract so the Android client can be built against it.

---

## Goals

- Provide stable endpoints for:
  - live matches
  - upcoming fixtures (next 24 hours)
- Ensure responses are consistent, cache-friendly, and do not leak provider shapes.
- Add “protocol used” metadata so the client can display HTTP/3 vs fallback.

---

## Deliverables

API:

- `GET /api/matches/live` returns a stable response envelope for live fixtures.
- `GET /api/matches/upcoming` returns upcoming fixtures within the next 24 hours.
- Keep `/api/live-matches*` temporarily as a compatibility alias or remove it with a documented breaking change.

Data:

- API-Football integration extended to support upcoming fixtures.
- Caching strategy implemented per endpoint and per provider quota constraints.

Contract:

- Create DTOs under `Contracts/`:
  - `MatchListResponse`
  - `MatchSummary`
  - `TeamSummary`
  - `Meta` with fields like `protocol`, `cachedAtUtc`, `source`
- Standardize identifiers:
  - expose upstream fixture ids as the primary `matchId` in the public contract
  - keep internal GUIDs only if needed for storage, never as the sole identifier clients must guess

---

## Implementation Notes (Best Practices)

- Add a provider client layer:
  - typed HttpClient or named client plus a thin client wrapper
  - provider response models local to the provider folder
  - mapping into internal domain models and then into contract DTOs
- Use `ProblemDetails` for:
  - missing API key
  - upstream errors
  - rate limiting
- Add rate limiting for public endpoints (per IP) to protect upstream quotas.
- Add health checks that can report “degraded” when upstream is unavailable.

---

## Acceptance Criteria

- Endpoints return stable JSON shapes documented in TRD.
- Cache hit rate is measurable (logs or basic metrics).
- Upstream 429 and 5xx are handled without crashing; responses degrade gracefully.
- Client can reliably show:
  - whether HTTP/3 was attempted and whether it succeeded
  - if response was cached and when it was generated
