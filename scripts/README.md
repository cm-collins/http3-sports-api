# Scripts

These scripts run the API and call endpoints the way a real user (or Android client) would.

Defaults:

- Uses `http://localhost:5000` (set `USE_HTTPS=1` to use `https://localhost:5001` with `-k`)
- Starts the API in the background if it is not already running
- Stops the API when the script exits

Provider notes:

- Match endpoints require API-Football to be configured (`ApiFootball__ApiKey` or user-secrets), otherwise they return 503.
- Highlights are backed by ScoreBat and may return a degraded response if upstream blocks or fails.

Configure API-Football (choose one):

```bash
export ApiFootball__ApiKey="YOUR_KEY"
# or:
dotnet user-secrets set "ApiFootball:ApiKey" "YOUR_KEY" --project LiveMatchApi.csproj
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
