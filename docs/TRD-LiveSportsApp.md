# Technical Requirements Document (TRD)
## Live Match Companion App
**Version:** 1.0  
**Status:** Draft  
**Last Updated:** 2026-03-30

---

Phased execution plans live in `docs/plans/README.md`.

## Table of Contents
1. [System Overview](#1-system-overview)
2. [Architecture](#2-architecture)
3. [Technology Stack](#3-technology-stack)
4. [External APIs](#4-external-apis)
5. [Backend Requirements (.NET 10)](#5-backend-requirements-net-10)
6. [Mobile Requirements (Kotlin Android)](#6-mobile-requirements-kotlin-android)
7. [HTTP/3 Implementation](#7-http3-implementation)
8. [Data Models](#8-data-models)
9. [API Contract](#9-api-contract)
10. [Error Handling & Resilience](#10-error-handling--resilience)
11. [Security Requirements](#11-security-requirements)
12. [Infrastructure](#12-infrastructure)
13. [Testing Strategy](#13-testing-strategy)

---

## 1. System Overview

### 1.1 High-Level System Diagram

```mermaid
graph LR
    subgraph External["External Data Sources"]
        AF[API-Football\nLive Scores & Stats]
        SB[ScoreBat\nVideo Highlights]
        TDB[TheSportsDB\nTeam Logos & Meta]
    end

    subgraph Backend[".NET 10 Backend (Kestrel)"]
        GW[API Gateway Layer]
        SC[Scores Controller]
        ST[Stats Controller]
        CM[Commentary Controller]
        HL[Highlights Controller]
        CA[Cache Layer\nIn-Memory]
        GW --> SC & ST & CM & HL
        SC & ST & CM & HL --> CA
    end

    subgraph Mobile["Android App (Kotlin)"]
        UI[Compose UI Layer]
        VM[ViewModels]
        REPO[Repository Layer]
        HTTP[HTTP/3 Client\nOkHttp + Ktor]
        UI --> VM --> REPO --> HTTP
    end

    AF -- "REST polling" --> SC
    AF -- "REST polling" --> ST
    SB -- "REST" --> HL
    TDB -- "REST" --> GW

    HTTP -- "QUIC/HTTP3\n5001 (dev) / 443 (prod)" --> GW

    subgraph Streams["4 Concurrent QUIC Streams"]
        S1[Stream 1: Score SSE]
        S2[Stream 2: Commentary SSE]
        S3[Stream 3: Stats]
        S4[Stream 4: Highlights]
    end
    HTTP --> Streams
```

### 1.2 Deployment Overview

```mermaid
graph TD
    subgraph Cloud["Cloud Host (must support UDP)"]
        BE[".NET 10 API\nKestrel HTTP/3\nPort 443 UDP + TLS"]
        BE --- CERT[TLS Certificate\nLet's Encrypt]
    end

    subgraph Device["Android Device"]
        APP["Kotlin App\nOkHttp QUIC Client"]
    end

    subgraph Network["Network Conditions"]
        WIFI[Wi-Fi]
        LTE[4G/LTE]
    end

    APP -- "QUIC over UDP" --> WIFI & LTE --> BE
    Note1["Connection ID persists\nacross network switch"]
    WIFI -.->|"IP changes"| LTE
```

### 1.3 Current Repository Snapshot (As Of 2026-03-30)

This repo currently contains a minimal backend only.

Environment (devcontainer):

- OS: Ubuntu 24.04 (container)
- .NET SDK: 10.0.200
- Target framework: `net10.0`

Backend behavior today:

- Listens on `http://0.0.0.0:5000` (HTTP/1.1).
- Listens on `https://0.0.0.0:5001` (HTTP/1.1 + HTTP/2 + HTTP/3) only if a valid localhost dev certificate is found and QUIC is supported in the runtime environment.
- Uses API-Football for real live-match data when configured. If the API key is missing, the live match endpoints return 503.

Implemented endpoints today:

- `GET /` (service info)
- `GET /health`
- `GET /api/live-matches`
- `GET /api/live-matches/{id}`

---

## 2. Architecture

### 2.1 Architecture Pattern

| Layer | Pattern | Reason |
|---|---|---|
| Android | MVVM + Repository | Separation of concerns, testability |
| Backend | Layered MVC | Clear routing, middleware chain |
| Data flow | Reactive streams (Flow/SSE) | Real-time push from server |
| Caching | In-memory (IMemoryCache) | Reduce external API calls, stay within free tiers |

### 2.2 Layered Architecture — Android

```mermaid
graph TD
    subgraph Presentation["Presentation Layer"]
        MA[Match List Screen]
        MD[Match Detail Screen]
        HF[Highlights Feed Screen]
        BM[Benchmark Dashboard]
    end

    subgraph ViewModel["ViewModel Layer"]
        MLV[MatchListViewModel]
        MDV[MatchDetailViewModel]
        HFV[HighlightsFeedViewModel]
        BMV[BenchmarkViewModel]
    end

    subgraph Repository["Repository Layer"]
        MR[MatchRepository]
        HR[HighlightsRepository]
        BR[BenchmarkRepository]
    end

    subgraph Network["Network Layer"]
        H3C[HTTP/3 Client\nOkHttp QUIC]
        H2C[HTTP/2 Client\nbenchmark comparison]
        SSE[SSE Stream Handler]
    end

    MA --> MLV --> MR
    MD --> MDV --> MR
    HF --> HFV --> HR
    BM --> BMV --> BR

    MR & HR & BR --> H3C & H2C
    H3C --> SSE
```

### 2.3 Backend Layer Architecture

```mermaid
graph TD
    subgraph Request["Incoming Request"]
        REQ[QUIC/HTTP3 Request]
    end

    subgraph Middleware["Middleware Pipeline"]
        CORS[CORS Middleware]
        LOG[Request Logging]
        CACHE[Cache Check]
        AUTH[Alt-Svc Header Injection\n(planned)]
    end

    subgraph Controllers["Controllers"]
        SC[ScoresController]
        STC[StatsController]
        CC[CommentaryController]
        HC[HighlightsController]
        BENCH[BenchmarkController]
    end

    subgraph Services["Service Layer"]
        SS[ScoresService]
        STS[StatsService]
        CS[CommentaryService]
        HS[HighlightsService]
    end

    subgraph External["External APIs"]
        AF[API-Football]
        SB[ScoreBat]
    end

    REQ --> CORS --> LOG --> CACHE --> AUTH
    AUTH --> SC & STC & CC & HC & BENCH
    SC --> SS --> AF
    STC --> STS --> AF
    CC --> CS
    HC --> HS --> SB
```

---

## 3. Technology Stack

### 3.1 Backend

| Component | Technology | Version | Reason |
|---|---|---|---|
| Runtime | .NET | 10.0 | Native HTTP/3 support in Kestrel |
| Web framework | ASP.NET Core | 10.0 | Built-in SSE, minimal APIs |
| HTTP server | Kestrel | 10.0 | QUIC/HTTP3 via `HttpProtocols.Http1AndHttp2AndHttp3` |
| TLS | TLS 1.3 | — | Required by QUIC spec |
| Caching | IMemoryCache | Built-in | Rate limit buffer for free API tiers |
| HTTP client | HttpClient + IHttpClientFactory | Built-in | Calls to external APIs |
| Serialisation | System.Text.Json | Built-in | Fast, allocation-friendly |

### 3.2 Android

| Component | Technology | Version | Reason |
|---|---|---|---|
| Language | Kotlin | 1.9+ | Modern, coroutine-native |
| UI | Jetpack Compose | Latest stable | Declarative, reactive |
| HTTP client | OkHttp | 5.0.0-alpha | HTTP/3 / QUIC support |
| HTTP client wrapper | Ktor Client (OkHttp engine) | 2.3.x | Kotlin-idiomatic API |
| Video player | ExoPlayer (Media3) | 1.3.x | HLS, streaming, ExoPlayer |
| DI | Hilt | 2.x | Kotlin-native DI |
| Async | Kotlin Coroutines + Flow | 1.7.x | SSE stream as Flow |
| Navigation | Navigation Compose | Latest | Screen routing |
| Charts | Vico / MPAndroidChart | Latest | Benchmark latency graphs |

---

## 4. External APIs

### 4.1 API Summary

| API | Purpose | Free Tier | Auth |
|---|---|---|---|
| API-Football (api-sports.io) | Live scores, fixtures, stats, lineups | 100 requests/day | API Key (header) |
| ScoreBat Video API | Goal highlight embed URLs | Unlimited (basic feed) | None (basic) |
| TheSportsDB | Team logos, competition metadata | Free (Patreon tier) | None |

### 4.2 API Data Flow

```mermaid
sequenceDiagram
    participant App as Android App
    participant BE as .NET Backend
    participant AF as API-Football
    participant SB as ScoreBat
    participant TDB as TheSportsDB

    App->>BE: GET /api/matches/live (HTTP/3)
    BE->>AF: GET /fixtures?live=all
    AF-->>BE: Fixture list JSON
    BE->>TDB: GET /api/v1/json/3/search_all_teams.php
    TDB-->>BE: Team logo URLs
    BE-->>App: Enriched match list (HTTP/3)

    App->>BE: GET /api/match/{id}/stream (SSE over HTTP/3)
    loop Every 15s
        BE->>AF: GET /fixtures?id={id}
        AF-->>BE: Updated score + stats
        BE-->>App: SSE event: score_update
    end

    App->>BE: GET /api/highlights/{team} (HTTP/3)
    BE->>SB: GET /video-api/v3/team/{team}
    SB-->>BE: Highlight embed URLs
    BE-->>App: Highlights list (HTTP/3)
```

### 4.3 Caching Strategy

| Data Type | Cache Duration | Reason |
|---|---|---|
| Live score | 10 seconds | Balance freshness vs API quota |
| Player stats | 30 seconds | Stats change less frequently |
| Team logos | 24 hours | Static assets |
| Highlights | 5 minutes | New clips added infrequently |
| Fixtures list | 60 seconds | Upcoming matches don't change fast |

---

## 5. Backend Requirements (.NET 10)

### 5.1 Kestrel HTTP/3 Configuration

```mermaid
flowchart TD
    K[Kestrel Server Starts]
    K --> P1[Port 5000\nHTTP/1.1\nTCP]
    K --> P2[Port 5001\nHTTP/1.1 + HTTP/2 + HTTP/3\nTCP + UDP]
    P2 --> TLS[TLS 1.3 Certificate\nRequired for QUIC]
    TLS --> ALT[Alt-Svc Header\n(planned for discovery)]
    ALT --> NOTE["Tells clients:\n'HTTP/3 available on :5001'"]
```

### 5.2 Endpoints

#### 5.2.1 Current Endpoints

| Method | Path | Protocol | Description |
|---|---|---|---|
| GET | `/api/live-matches` | HTTP/2 or HTTP/3 | List live matches (API-Football; returns 503 if not configured) |
| GET | `/api/live-matches/{id}` | HTTP/2 or HTTP/3 | Get one match (API-Football; returns 503 if not configured) |

#### 5.2.2 Target (Planned) Endpoints

| Method | Path | Protocol | Description |
|---|---|---|---|
| GET | `/api/matches/live` | HTTP/3 | All currently live matches |
| GET | `/api/matches/upcoming` | HTTP/3 | Next 24h fixtures |
| GET | `/api/match/{id}/stream` | SSE over HTTP/3 | Real-time score + events |
| GET | `/api/match/{id}/stats` | HTTP/3 | Player & team statistics |
| GET | `/api/highlights/feed` | HTTP/3 | Recent highlights (all leagues) |
| GET | `/api/highlights/{team}` | HTTP/3 | Team-specific highlights |
| GET | `/api/benchmark/ping` | HTTP/3 + HTTP/2 | Latency test endpoint |
| GET | `/api/benchmark/payload/{kb}` | HTTP/3 + HTTP/2 | Payload size test |

### 5.3 SSE Stream Design

```mermaid
sequenceDiagram
    participant App as Kotlin App
    participant BE as .NET SSE Endpoint

    App->>BE: GET /api/match/{id}/stream\nAccept: text/event-stream
    BE-->>App: HTTP 200\nContent-Type: text/event-stream

    loop Every 10-15 seconds
        BE-->>App: event: score_update\ndata: {"home":1,"away":0,"minute":34}
    end

    loop On goal event
        BE-->>App: event: goal\ndata: {"scorer":"Player Name","minute":34,"team":"home"}
    end

    loop On match end
        BE-->>App: event: match_end\ndata: {"result":"1-0","winner":"home"}
        BE-->>App: [Connection closes cleanly]
    end

    Note over App,BE: Single long-lived HTTP/3 stream\nper match subscription
```

---

## 6. Mobile Requirements (Kotlin Android)

### 6.1 Screen Map

```mermaid
graph LR
    SPLASH[Splash Screen] --> LIST[Match List\nScreen]
    LIST --> DETAIL[Match Detail\nScreen]
    LIST --> HIGHLIGHTS[Highlights\nFeed Screen]
    DETAIL --> VIDEO[Video Player\nScreen]
    HIGHLIGHTS --> VIDEO
    LIST --> BENCH[Benchmark\nDashboard]

    style SPLASH fill:#1a1a2e,color:#fff
    style LIST fill:#16213e,color:#fff
    style DETAIL fill:#0f3460,color:#fff
    style HIGHLIGHTS fill:#533483,color:#fff
    style VIDEO fill:#0f3460,color:#fff
    style BENCH fill:#1a1a2e,color:#fff
```

### 6.2 Match Detail — Concurrent Stream Loading

```mermaid
flowchart TD
    OPEN[User Opens Match Detail]
    OPEN --> QUIC[Single QUIC Connection Established]
    QUIC --> S1
    QUIC --> S2
    QUIC --> S3
    QUIC --> S4

    subgraph Streams["4 Independent QUIC Streams"]
        S1[Stream 1\nLive Score SSE\nFlow&lt;ScoreEvent&gt;]
        S2[Stream 2\nCommentary SSE\nFlow&lt;CommentaryEvent&gt;]
        S3[Stream 3\nStats Polling\nevery 30s]
        S4[Stream 4\nHighlights Fetch\none-shot + refresh]
    end

    S1 --> UI1[Score Panel\nupdates independently]
    S2 --> UI2[Commentary Feed\nupdates independently]
    S3 --> UI3[Stats Panel\nupdates independently]
    S4 --> UI4[Highlights Row\nupdates independently]

    NOTE["If Stream 3 has packet loss:\nStreams 1, 2, 4 are unaffected"]
    style NOTE fill:#2d6a4f,color:#fff
```

### 6.3 HTTP Client Selection Logic

```mermaid
flowchart TD
    START[App Initialises HTTP Client]
    START --> CHECK{Benchmark Mode?}
    CHECK -- Yes --> BOTH[Instantiate both\nHTTP/3 Client\nHTTP/2 Client]
    CHECK -- No --> H3[Instantiate\nHTTP/3 Client only]

    BOTH --> BENCH[BenchmarkRepository uses both\nand records latency per call]
    H3 --> NORMAL[All requests via HTTP/3\nHTTP/2 fallback if QUIC blocked]

    NORMAL --> DETECT{Protocol\nactually used?}
    DETECT -- HTTP/3 --> BADGE["Show: QUIC ✅"]
    DETECT -- HTTP/2 --> BADGE2["Show: HTTP/2 fallback ⚠️"]
```

### 6.4 Network Migration Handling

```mermaid
stateDiagram-v2
    [*] --> WIFI_CONNECTED : App launches on Wi-Fi

    WIFI_CONNECTED --> STREAMING : QUIC streams active
    STREAMING --> NETWORK_CHANGE : Device switches network

    NETWORK_CHANGE --> QUIC_MIGRATION : QUIC sends PATH_CHALLENGE
    QUIC_MIGRATION --> STREAMING : Path confirmed, streams resume
    QUIC_MIGRATION --> RECONNECT : Migration fails (fallback)

    RECONNECT --> STREAMING : Re-establish QUIC connection
    STREAMING --> OFFLINE : No network available

    OFFLINE --> CACHED_STATE : Show cached last-known scores
    CACHED_STATE --> STREAMING : Network restored
```

---

## 7. HTTP/3 Implementation

### 7.1 QUIC Handshake Flow

```mermaid
sequenceDiagram
    participant App as Android App
    participant BE as .NET Kestrel

    Note over App,BE: First connection (1-RTT)
    App->>BE: QUIC Initial (ClientHello + TLS 1.3)
    BE-->>App: QUIC Handshake (ServerHello + Certificate)
    App->>BE: QUIC Handshake Complete
    App->>BE: HTTP/3 HEADERS frame (first request)
    BE-->>App: HTTP/3 response

    Note over App,BE: Returning connection (0-RTT)
    App->>BE: 0-RTT data (request sent immediately)
    BE-->>App: Response (no round trip wait)
```

### 7.2 HTTP/3 vs HTTP/2 — Head-of-Line Blocking

```mermaid
flowchart TD
    subgraph http2["HTTP/2 over TCP — Packet Loss Scenario"]
        direction LR
        P1_2[Score packet ❌ lost] --> STALL[TCP stalls\nALL streams]
        STALL --> W1[Score waits...]
        STALL --> W2[Commentary waits...]
        STALL --> W3[Stats wait...]
        STALL --> W4[Highlights wait...]
        W1 & W2 & W3 & W4 --> RETX[Retransmit completes]
        RETX --> RESUME[All resume together]
    end

    subgraph http3["HTTP/3 over QUIC — Packet Loss Scenario"]
        direction LR
        P1_3[Score packet ❌ lost] --> RETX3[Score stream retransmits]
        RETX3 --> OK1[Score resumes]
        P1_3 -.->|"No effect"| OK2[Commentary ✅ unaffected]
        P1_3 -.->|"No effect"| OK3[Stats ✅ unaffected]
        P1_3 -.->|"No effect"| OK4[Highlights ✅ unaffected]
    end
```

---

## 8. Data Models

### 8.1 Core Entities

```mermaid
erDiagram
    MATCH {
        string id
        string homeTeam
        string awayTeam
        int homeScore
        int awayScore
        int minute
        string status
        string competition
        datetime kickoff
    }

    TEAM {
        string id
        string name
        string logoUrl
        string country
    }

    PLAYER_STATS {
        string matchId
        string playerId
        string playerName
        int minutesPlayed
        int shots
        int shotsOnTarget
        int passes
        float passAccuracy
        int tackles
        int fouls
    }

    COMMENTARY_EVENT {
        string matchId
        int minute
        string type
        string text
        string playerId
    }

    HIGHLIGHT {
        string id
        string matchId
        string title
        string embedUrl
        string thumbnailUrl
        int minute
        string competition
        datetime publishedAt
    }

    BENCHMARK_RESULT {
        string protocol
        string endpoint
        long latencyMs
        int statusCode
        datetime recordedAt
        bool connectionMigrated
    }

    MATCH ||--|| TEAM : "home"
    MATCH ||--|| TEAM : "away"
    MATCH ||--o{ PLAYER_STATS : "has"
    MATCH ||--o{ COMMENTARY_EVENT : "has"
    MATCH ||--o{ HIGHLIGHT : "has"
```

---

## 9. API Contract

### 9.1 Response — Live Match List

```
GET /api/matches/live
Protocol: HTTP/3
Response: 200 OK

{
  "matches": [
    {
      "id": "string",
      "homeTeam": { "id": "string", "name": "string", "logoUrl": "string" },
      "awayTeam": { "id": "string", "name": "string", "logoUrl": "string" },
      "homeScore": 0,
      "awayScore": 0,
      "minute": 0,
      "status": "LIVE | UPCOMING | FT",
      "competition": "string",
      "kickoff": "ISO8601"
    }
  ],
  "meta": {
    "protocol": "HTTP/3",
    "cachedAt": "ISO8601",
    "source": "api-football"
  }
}
```

### 9.2 SSE Stream Events

```
GET /api/match/{id}/stream
Accept: text/event-stream
Protocol: HTTP/3

# Score update event
event: score_update
data: {"homeScore":1,"awayScore":0,"minute":34}

# Goal event
event: goal
data: {"scorer":"Player Name","team":"home","minute":34,"assistBy":"Assist Name"}

# Stats update event
event: stats_update
data: {"possession":{"home":58,"away":42},"shots":{"home":7,"away":3}}

# Commentary event
event: commentary
data: {"minute":34,"text":"GOAL! A player scores!","type":"GOAL"}

# Match end
event: match_end
data: {"result":"1-0","winner":"home","minute":90}
```

---

## 10. Error Handling & Resilience

### 10.1 Error & Recovery Flow

```mermaid
flowchart TD
    REQ[HTTP/3 Request Sent] --> TRY{Response OK?}

    TRY -- Yes --> SUCCESS[Process Response]
    TRY -- No --> ERR{Error Type?}

    ERR -- "Network timeout" --> RETRY[Retry with\nexponential backoff\nmax 3 attempts]
    ERR -- "QUIC blocked by firewall" --> FALLBACK[Fallback to HTTP/2\nlog protocol downgrade]
    ERR -- "External API 429" --> CACHE[Serve cached response\nshow staleness indicator]
    ERR -- "External API 503" --> DEGRADE[Graceful degradation\nhide affected panel]
    ERR -- "No network" --> OFFLINE[Show offline state\ncached data]

    RETRY --> TRY
    FALLBACK --> SUCCESS
    CACHE --> SUCCESS
    DEGRADE --> UI[Update UI with\npartial data]
    OFFLINE --> CACHED[Show last-known\ncached state]
```

### 10.2 Stream Failure Isolation

Individual QUIC stream failures must not crash the app or affect other streams:

| Stream | Failure Behaviour |
|---|---|
| Score stream fails | Show last known score, add stale indicator |
| Commentary stream fails | Hide commentary panel, no crash |
| Stats stream fails | Show skeleton state, retry silently |
| Highlights stream fails | Show "No highlights yet", no crash |
| All streams fail | Show offline mode with cached match data |

---

## 11. Security Requirements

| Requirement | Detail |
|---|---|
| Transport security | TLS 1.3 mandatory (enforced by QUIC) |
| API keys | Stored in environment variables (for example `ApiFootball__ApiKey`), never in source code |
| Certificate | Valid TLS cert required on deployed server (Let's Encrypt) |
| Local dev cert | .NET dev certificate for localhost testing |
| No PII collected | App collects no user data in v1.0 |
| API key rotation | Keys stored in environment variables; local `.env` is optional (must be gitignored if used) |

---

## 12. Infrastructure

Go-live definitions and rollout checklist live in `docs/GO-LIVE.md`.

### 12.1 Deployment Architecture

```mermaid
graph TD
    subgraph Local["Local Development"]
        DEV[".NET 10 Dev Server\nlocalhost:5001\nDev cert"]
        EMU["Android Emulator\n10.0.2.2 → localhost"]
        DEV <--> EMU
    end

    subgraph Staging["Staging (Public Host)"]
        STG[".NET 10 API\nKestrel HTTP/3\n443/udp + TLS"]
        CERT2["TLS Certificate\n(ACME/Let's Encrypt)"]
        STG --- CERT2
    end

    subgraph Device["Physical Testing"]
        PHONE["Android Device\nSame network or\nmobile data"]
        PHONE --> STG
    end

    Note1["Inbound UDP must be open\n(e.g. 443/udp in prod)\nfor end-to-end HTTP/3"]
    style Note1 fill:#e63946,color:#fff
```

### 12.2 Repository Structure

```
http3-sports-api/
├── Program.cs
├── LiveMatchApi.csproj
├── LiveMatchApi.http
├── appsettings.json
├── appsettings.Development.json
├── Models/
├── Services/
├── Properties/
├── docs/
│   ├── PRD-LiveSportsApp.md
│   ├── TRD-LiveSportsApp.md
│   ├── GO-LIVE.md
│   ├── TLS-UDP-QUIC-HTTP3.md
│   └── plans/
└── results/
    └── benchmarks.md
```

---

## 13. Testing Strategy

### 13.1 Test Types

| Test Type | What is Tested | Tool |
|---|---|---|
| Unit | Services, repositories, ViewModels | xUnit (.NET), JUnit (Kotlin) |
| Integration | .NET endpoints respond correctly | WebApplicationFactory |
| Protocol | HTTP/3 actually negotiated | Wireshark / Charles Proxy |
| Benchmark | HTTP/2 vs HTTP/3 latency | In-app dashboard |
| Network simulation | Packet loss, network switch | Android Network Link Conditioner |
| Chaos | Kill individual streams | Block one API mid-session manually |

### 13.2 Benchmark Test Plan

```mermaid
flowchart LR
    START[Open Benchmark Screen]
    START --> MODE{Select Mode}

    MODE --> H3[HTTP/3 Mode]
    MODE --> H2[HTTP/2 Mode]

    H3 & H2 --> TESTS

    subgraph TESTS["Run Tests"]
        T1[Ping x 10\nMeasure avg latency]
        T2[Payload 100KB x 5]
        T3[Payload 1MB x 3]
        T4[Open 4 streams\nSimultaneously]
        T5[Switch network\nMid-stream]
    end

    TESTS --> RESULTS[Display Results\nBar chart comparison\nWinner highlighted]
```

### 13.3 Acceptance Criteria

| Scenario | Expected Result |
|---|---|
| App connects to server | HTTP/3 negotiated (verify via protocol label) |
| Match detail opens | All 4 panels load within 3 seconds |
| Network switches mid-match | Score stream continues within 1 second |
| One stream has packet loss | Other 3 streams continue unaffected |
| HTTP/3 vs HTTP/2 benchmark | HTTP/3 shows lower latency on mobile |
| External API is down | App shows cached data, no crash |
| QUIC blocked (firewall) | App falls back to HTTP/2 gracefully |
