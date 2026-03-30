# Product Requirements Document (PRD)
## Live Match Companion (HTTP/3 Showcase)

**Version:** 1.0  
**Status:** Draft  
**Last Updated:** 2026-03-30

---

## 1. Summary

Live Match Companion is a live sports experience (football-first) designed to demonstrate the real, user-visible benefits of HTTP/3 (QUIC) compared to HTTP/2 on mobile networks: lower tail latency under loss, smoother concurrent loading, and better behavior during network changes.

This repo currently contains only the backend API skeleton and planning docs. The Android client is a later phase.

---

## 2. Problem Statement

During live matches, fans frequently experience:

- Slow or stalled updates under packet loss.
- UI panels loading sequentially instead of concurrently (score, stats, highlights).
- Connection interruptions when switching Wi-Fi to cellular.

We want a product where those issues are measurable and demonstrably improved with HTTP/3.

---

## 3. Goals

- Provide a fast, reliable live match API that can serve multiple data “panels” concurrently.
- Offer real-time updates (server push) so the client doesn’t rely purely on polling.
- Include a simple benchmark mode to compare HTTP/2 vs HTTP/3 under the same conditions.
- Be operable as a public service (“live”) with basic reliability, security, and observability.

---

## 4. Non-Goals (v1)

- User accounts, profiles, or any PII collection.
- Payments or subscriptions.
- Multi-sport depth from day one (we’ll start football-first to keep upstream integration simple).
- Full parity with professional sports apps (this is a showcase + learning product).

---

## 5. Target Users

- Live sports fans who want quick updates and highlights.
- Engineers and learners exploring HTTP/3 behavior in real apps.
- Demo audiences (talks, blog posts, benchmarks).

---

## 6. Key User Experiences

- Match list: see currently live matches with score and minute.
- Match detail: score updates continuously; stats and highlights load independently.
- Highlights feed: browse recent clips.
- Benchmark dashboard: compare request latency and concurrent load across HTTP/2 vs HTTP/3; show which protocol was used.

---

## 7. MVP Scope (Backend)

### 7.1 API Features

- Live matches list (football-first).
- Upcoming fixtures (next 24 hours).
- Match stats endpoint (team + players as available).
- Highlights feed + team filter.
- Real-time score SSE stream (per match).
- Benchmark endpoints (ping + payload sizes + concurrency helper endpoints).

### 7.2 Product Behavior

- Caching to stay within free-tier upstream quotas.
- Graceful degradation:
  - If highlights provider fails, scores still work.
  - If stats are unavailable, return partial response and a clear status.
- Clear “protocol used” metadata in responses (so the UI can show HTTP/3 vs fallback).

---

## 8. MVP Scope (Client)

Not part of this repo yet. Planned:

- Kotlin Android app (Compose) with a match list, match detail, highlights feed, benchmark dashboard.
- HTTP/3 client with HTTP/2 fallback and a visible protocol badge.

---

## 9. Success Metrics

Technical:

- p95 API latency is consistently lower on HTTP/3 than HTTP/2 in lossy/mobile conditions (measured in benchmark mode).
- Match detail loads multiple panels concurrently without blocking.
- SSE streams remain usable across typical network jitter; graceful reconnect when needed.

Product:

- Repeatable demo that shows protocol differences clearly.
- Clear “known limitations” documented (QUIC-blocked networks, upstream quotas).

---

## 10. Risks and Constraints

- QUIC may be blocked on some networks, forcing HTTP/2 fallback.
- Hosting must support inbound UDP for true end-to-end HTTP/3.
- Upstream APIs have rate limits and inconsistent data quality.

---

## 11. Milestones (Docs-Only Plan)

- Phase 0: Backend skeleton + docs aligned with reality (this repo state).
- Phase 1: Implement live matches + fixtures + caching.
- Phase 2: SSE streams + highlights + stats.
- Phase 3: Benchmark endpoints + results capture workflow.
- Phase 4: Android app + end-to-end demo.

