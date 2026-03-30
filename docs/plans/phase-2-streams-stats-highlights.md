# Phase 2: Streams, Stats, Highlights

**Last Updated:** 2026-03-30  
**Status:** Planned

This phase adds the features that make HTTP/3 benefits visible: server push, multi-panel concurrency, and resilience under loss.

---

## Goals

- Add real-time updates via SSE for match score and commentary.
- Add match stats and highlights endpoints with graceful degradation.
- Ensure independent “panels” can load concurrently without one blocking the others.

---

## Deliverables

SSE:

- `GET /api/match/{id}/stream` emits score updates and key events.
- Optional: split into multiple SSE streams if it improves clarity:
  - `/api/match/{id}/score-stream`
  - `/api/match/{id}/commentary-stream`

Stats:

- `GET /api/match/{id}/stats` returns team and player stats when available.
- Caching tuned for stats update frequency.

Highlights:

- `GET /api/highlights/feed` returns recent highlights.
- `GET /api/highlights/{team}` returns team-specific highlights.

---

## Implementation Notes (Best Practices)

- Keep SSE implementation isolated:
  - a service that produces events
  - endpoint only writes events, does not contain business logic
- Backpressure and reconnection:
  - handle client disconnect cleanly via `CancellationToken`
  - include event ids so clients can resume if needed
- Treat each upstream as optional:
  - if highlights provider fails, scores still work
  - return partial responses with a clear status field

---

## Acceptance Criteria

- Client can open a match detail screen and load:
  - score stream
  - commentary stream
  - stats
  - highlights
  concurrently without visible blocking.
- Under packet loss simulation, other panels continue to update while one is delayed.
- SSE endpoints do not leak memory or hold resources after disconnect.

