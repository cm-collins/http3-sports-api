# http3-sports-api

Starter ASP.NET Core API scaffolded for HTTP/1.1, HTTP/2, and HTTP/3 development inside a Dev Container.

## Structure

- `LiveMatchApi.csproj`: root-level ASP.NET Core project targeting `.NET 10`
- `Program.cs`: minimal API startup and Kestrel HTTP/3 configuration
- `Models/` and `Services/`: simple starter domain and in-memory data access
- `.devcontainer`: VS Code Dev Container configuration using the official .NET 10 image

## Quick Start

### In the Dev Container

1. Rebuild and reopen the project in the Dev Container.
2. Restore packages:

   ```bash
   dotnet restore
   ```

3. Run the API:

   ```bash
   dotnet run
   ```

### Local Endpoints

- `GET /`
- `GET /health`
- `GET /api/live-matches`
- `GET /api/live-matches/{id}`

By default the app listens on:

- `http://0.0.0.0:5000`
- `https://0.0.0.0:5001`

Port `5001` is enabled automatically when a localhost development certificate is available. In the Dev Container, the post-create hook prepares that certificate for you. If no dev certificate is present, the API still starts on `5000` and logs that HTTPS/HTTP3 is disabled.

In VS Code Dev Containers, TCP forwarding works normally, while full external QUIC/UDP testing still needs host networking or running outside the container.
