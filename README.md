# HamStats

A live amateur-radio contest scoreboard. HamStats listens to UDP broadcasts from the
[N1MM+](https://n1mmwleplus.groups.io/) contest logger, stores them in a SQLite database, and
serves a real-time web dashboard showing radios, contacts, and the running score breakdown — built
for multi-operator events like ARRL Field Day.

It's designed to run as a **passive wall/kiosk display** — left up on a screen for everyone to
watch, with no mouse or keyboard needed during operation. Admin functions (a live N1MM **packet
log** and **settings**, including the local time zone used for all displayed times) are tucked
behind the app-bar menu so the dashboard stays clean for viewing.

## How it works

```
N1MM+ logger ──UDP:12060──▶ HamStats.Website ──▶ SQLite
   (XML broadcasts)         (N1MMWatcher)             │
                                  │                    │
                            /api/v0/* ◀───────────────┘
                                  │
                            HamStats.Vue (dashboard, polls every 1s)
```

N1MM+ is configured to broadcast contact, radio, and score messages over UDP. The `N1MMWatcher`
background service receives them on port 12060, parses the XML, and persists it. The dashboard
polls the API once per second to stay current.

## Components

| Project           | Stack                          | Role                                              |
| ----------------- | ------------------------------ | ------------------------------------------------- |
| `HamStats.Website`| ASP.NET Core 10                | UDP ingestion, `/api/v0/*` API, serves the SPA    |
| `HamStats.Data`   | EF Core 10 + SQLite            | Database context and entity models                |
| `HamStats.Vue`    | Vue 3 + Vuetify 3 + Vite       | Real-time dashboard frontend                      |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (for the Vue frontend)
- [Docker](https://www.docker.com/) (only to run the published container — not needed for local development)

## Running with Docker (recommended)

The published image bundles the backend, the built dashboard, and an embedded SQLite database.
Copy `docker-compose.yml` and `.env.example` from the repo, then:

```bash
cp .env.example .env
# Optional: edit .env to change ports
docker compose up -d
```

The dashboard is then available at `http://localhost` and N1MM+ broadcasts are received on UDP
port `12060`. Point N1MM+ at this host's IP / port `12060` and contacts will appear as they're
logged. The SQLite database is persisted in a `data/` directory alongside the compose file.

> The `stable` tag always points to the latest release. Set `VERSION` in `.env` to pin a version.

## Local development

No database server is required — the backend creates a local `hamstats.db` SQLite file on startup.

1. **Run the backend** (serves on `http://localhost:5000`, OpenAPI doc at `/openapi/v1.json`):

   ```bash
   dotnet run --project HamStats.Website
   ```

2. **Run the frontend** (dev server on `http://localhost:3001`, proxies `/api` to the backend):

   ```bash
   cd HamStats.Vue
   npm install
   npm run dev
   ```

3. **Point N1MM+ at it:** configure N1MM+ to broadcast over UDP to this machine's IP on port
   `12060` (N1MM+'s default broadcast port). Contacts and scores will appear on the dashboard as
   they're logged.

## Configuration

- **Database connection** — `HamStats.Website/appsettings.json` (`ConnectionStrings:Default`); defaults
  to a local `hamstats.db` file, overridden to `/data/hamstats.db` in the container.
- **UDP listen port** — `12060` (N1MM+ default); override with `N1MM:BroadcastPort` in `appsettings.json`.
- **Backend URL** — `http://localhost:5000`, set in `appsettings.json` (Kestrel).

## Note on data persistence

The database is **dropped and recreated on every startup** — all data is cleared each time the
backend restarts. This is intentional during early development for these ephemeral contest events.
Schema changes are made by editing the model classes directly; EF migrations will be adopted once
early development settles down.

## Building the image from source

```bash
docker build -t hamstats:local \
  --build-arg version=0.0.1 \
  --build-arg gitsha=$(git rev-parse HEAD) .
```

## Building for production

```bash
dotnet publish HamStats.sln                  # backend
cd HamStats.Vue && npm run build             # frontend → HamStats.Vue/build/
```
