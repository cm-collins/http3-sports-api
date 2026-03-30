# Engineering Guidelines (.NET 10)

**Last Updated:** 2026-03-30

This is a working set of conventions for keeping the backend clean and maintainable as we add features.

---

## API Design

- Prefer stable API contracts and version explicitly when needed (path or header based).
- Do not leak upstream provider response shapes directly to clients.
- Use consistent response envelopes where it helps: `data` plus optional `meta` and `errors`.
- Use `ProblemDetails` consistently for errors; avoid ad-hoc anonymous error objects.

---

## Code Structure

- Keep external integrations isolated:
  - `Services/Providers/ApiFootball/` for provider-specific HTTP client + models
  - mapping into internal domain models in one place
- Keep public API models separate from internal models:
  - `Models/` for domain
  - `Contracts/` for request/response DTOs
- Prefer small focused services over one “mega service”.

---

## HTTP Client Best Practices

- Use `IHttpClientFactory` with named or typed clients.
- Set a base address and default headers once in DI wiring.
- Set timeouts and handle transient failures with retries and backoff.
- Respect upstream rate limits:
  - cache aggressively for read-mostly data
  - avoid duplicate calls per request

---

## Options and Configuration

- Use options binding:
  - `builder.Services.AddOptions<T>().BindConfiguration("Section").ValidateOnStart()`
- Secrets must come from environment variables in staging and prod.
- Prefer `ApiFootball__ApiKey` style env vars for container friendliness.

---

## Cancellation and Time

- Accept `CancellationToken` in every endpoint handler and every downstream call.
- Store times as UTC (`DateTimeOffset`) and convert at the edge (client).

---

## Logging and Observability

- Log at boundaries:
  - request start and end (middleware or minimal API filter)
  - provider call success/failure with latency
- Add metrics as soon as we go beyond a demo:
  - request latency, error rate, cache hit rate, upstream call count

---

## Testing Strategy

- Unit tests:
  - parsing and mapping from provider JSON
  - cache behavior and error handling
- Integration tests:
  - endpoint contract correctness
  - HTTP/3 negotiation validation is primarily an environment test, but endpoint behavior can be tested.

---

## QUIC and HTTP/3 Notes

- HTTP/3 requires TLS and UDP reachability.
- Clients must gracefully fall back to HTTP/2 when QUIC is blocked.
- Document whether you are benchmarking client-to-origin or client-to-edge if a proxy or CDN is involved.

