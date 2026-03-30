# Go-Live Plan (Backend)

**Last Updated:** 2026-03-30  
**Scope:** Backend API only (this repo). Android app is a later phase.

This document defines what “live” means for `http3-sports-api`, plus a practical checklist to get there.

---

## What “Live” Means

The service is considered live when:

- A public HTTPS endpoint serves the API.
- HTTP/2 works reliably for all clients.
- HTTP/3 is available for clients that support QUIC, and we can measure it.
- External API integrations are resilient to rate limits and outages.
- Basic observability (logs + metrics) exists so we can operate it.

---

## HTTP/3 Reality Check (Non-Negotiables)

HTTP/3 requires:

- TLS (QUIC always uses TLS 1.3).
- UDP reachability from the client to the server on the chosen port (typically `443/udp` in production).

Common pitfalls:

- Many “easy” PaaS/load balancers only forward TCP; HTTP/3 won’t work end-to-end unless UDP is supported.
- If you put a reverse proxy/CDN in front, it may terminate QUIC. That can be fine, but it changes what you’re benchmarking (client-to-edge vs client-to-origin).

---

## Environments

### Local (Devcontainer)

- Ports: `5000/tcp` (HTTP/1.1), `5001/tcp+udp` (HTTPS with HTTP/2 and HTTP/3).
- Note: VS Code forwards TCP only. HTTP/3 (UDP) can be tested inside the container. If your `curl` does not support HTTP/3, use `tools/ProtocolProbe`.

### Staging (Public)

Goals:

- Validate certificates, UDP reachability, and protocol negotiation from real networks.
- Exercise caching and rate limiting without impacting production.

### Production (Public)

Goals:

- Stable endpoints, predictable behavior, and dashboards/alerts.
- Gradual rollout and the ability to quickly revert to HTTP/2-only if needed.

---

## Configuration and Secrets (Planning)

We will need environment-based configuration for:

- External API keys (API-Football, etc.).
- TLS certificate configuration for Kestrel in production.
- Allowed origins (CORS) for the Android app and any web dashboard.

Constraints:

- No secrets in source control.
- Separate keys for staging vs production.

---

## Operational Requirements (Planning)

### Observability

- Request logs with a request id and protocol (HTTP/2 vs HTTP/3) where possible.
- Basic metrics: request rate, latency (p50/p95), error rate, cache hit rate, external API call rate.
- Health endpoint that reflects dependency status (at least “API up” vs “degraded”).

### Resilience

- In-memory caching to stay under free-tier quotas.
- Timeouts, retries with backoff, and circuit-breaker behavior around external APIs.
- Graceful degradation when highlights/stats providers are down.

### Security

- HTTPS everywhere in staging/prod.
- Tight CORS (only known client apps).
- Rate limiting to protect the service and upstream quotas.

---

## Release Checklist (Staging → Prod)

- Confirm chosen host supports inbound UDP on the production port (typically `443/udp`).
- Configure TLS certificate issuance and renewal.
- Verify protocol negotiation:
  - HTTP/2 works from a normal client.
  - HTTP/3 works from a QUIC-capable client/network (and fails over gracefully when blocked).
- Confirm external API quotas and caching behavior meet expected traffic.
- Add dashboards/alerts and run a load sanity test.
- Publish a “known limitations” section (QUIC blocked networks, free-tier rate limits, etc.).

---

## Phase Plan (Docs-Only)

- Phase 0 (now): .NET 10 foundation + HTTP/3-enabled dev environment + API-Football live matches (configured via env var).
- Phase 1: Add upcoming fixtures + stabilize API contracts + expand caching and resilience.
- Phase 2: SSE streams (scores/commentary) and highlights/stats endpoints.
- Phase 3: Benchmark endpoints + results capture workflow.
- Phase 4: Android app and user-facing benchmark dashboard.

Phase details live in `docs/plans/README.md`.
