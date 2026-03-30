# Phase 3: Benchmarks and Results Capture

**Last Updated:** 2026-03-30  
**Status:** Implemented (MVP)

This phase makes protocol differences measurable and repeatable.

---

## Goals

- Provide benchmark endpoints that can be called over HTTP/2 and HTTP/3.
- Record results in a consistent format for comparison.
- Keep benchmarks representative of real app behavior:
  - small pings
  - medium payloads
  - concurrent requests
  - long-lived streams

---

## Deliverables

Endpoints:

- `GET /api/benchmark/ping`
- `GET /api/benchmark/payload/{kb}`
- Optional: endpoints designed to be called concurrently by the client to simulate panels:
  - `GET /api/benchmark/panel/{name}?delayMs=...`
- Long-lived stream benchmark:
  - `GET /api/benchmark/stream?intervalMs=...`

Metadata:

- Responses include fields indicating:
  - protocol negotiated or attempted
  - server timing where available
  - cache status if applicable

Results:

- Update `results/benchmarks.md` with real run data and environment details.

---

## Implementation Notes (Best Practices)

- Keep benchmark endpoints isolated from product endpoints to avoid accidental coupling.
- Prevent abuse:
  - rate limit benchmark endpoints
  - cap payload sizes
- Avoid benchmarking through a proxy that changes the story unless that is the goal.

---

## Acceptance Criteria

- Android app or a CLI can run a benchmark suite end-to-end and store results.
- Results demonstrate expected behavior under:
  - good Wi-Fi
  - lossy mobile
  - network switches when possible
