# Phase 4: Android App and End-to-End Demo

**Last Updated:** 2026-03-30  
**Status:** Planned

This phase completes the user-visible product and the demo story.

---

## Goals

- Build the Android app against the stabilized backend contracts.
- Show concurrent loading and live updates in a match detail view.
- Provide a benchmark dashboard that compares HTTP/2 vs HTTP/3.

---

## Deliverables

Android:

- Screens:
  - Match list
  - Match detail
  - Highlights feed
  - Benchmark dashboard
- Networking:
  - HTTP/3 client with HTTP/2 fallback
  - clear protocol badge in the UI
- Streaming:
  - SSE consumed as Flow

Backend readiness:

- CORS configuration for the app environment
- “go live” checklist executed (see `docs/GO-LIVE.md`)

---

## Acceptance Criteria

- Match detail loads multiple panels concurrently and remains responsive.
- Protocol badge correctly shows HTTP/3 or fallback.
- Benchmark dashboard can run a suite and display results clearly.
- Demo can be repeated reliably across networks, with known limitations documented.

