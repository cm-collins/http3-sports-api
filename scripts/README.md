# Scripts

These scripts run the API and call endpoints the way a real user (or Android client) would.

Defaults:

- Uses `http://localhost:5000` (set `USE_HTTPS=1` to use `https://localhost:5001` with `-k`)
- Starts the API in the background if it is not already running
- Stops the API when the script exits

Credentials:

- Scripts start the API with `ASPNETCORE_ENVIRONMENT=Development` by default, so `appsettings.Development.json` is used.
- If `ApiFootball:ApiKey` is empty, match endpoints will return 503 (scripts will warn).
- If `ScoreBat:Token` is empty, highlights may degrade/403 depending on upstream (scripts will warn).

Recommended (don’t commit secrets): set keys via user-secrets:

```bash
dotnet user-secrets set "ApiFootball:ApiKey" "YOUR_KEY" --project LiveMatchApi.csproj
dotnet user-secrets set "ScoreBat:Token" "YOUR_TOKEN" --project LiveMatchApi.csproj
```

Run:

```bash
scripts/matches.sh
scripts/match-stats.sh 123456
STREAM_MODE=score scripts/match-stream.sh 123456
scripts/highlights.sh arsenal
scripts/benchmarks.sh
scripts/protocol-probe.sh
```
