# HamStats

A live amateur-radio contest scoreboard. HamStats listens to UDP broadcasts from the
[N1MM+](https://n1mmwleplus.groups.io/) contest logger, stores them in PostgreSQL, and serves a
real-time web dashboard showing radios, contacts, and the running score breakdown — built for
multi-operator events like ARRL Field Day.

## How it works

```
N1MM+ logger ──UDP:16000──▶ HamStats.Website ──▶ PostgreSQL
   (XML broadcasts)         (N1MMWatcher)              │
                                  │                     │
                            /api/v0/* ◀────────────────┘
                                  │
                            HamStats.Vue (dashboard, polls every 1s)
```

N1MM+ is configured to broadcast contact, radio, and score messages over UDP. The `N1MMWatcher`
background service receives them on port 16000, parses the XML, and persists it. The dashboard
polls the API once per second to stay current.

## Components

| Project           | Stack                          | Role                                              |
| ----------------- | ------------------------------ | ------------------------------------------------- |
| `HamStats.Website`| ASP.NET Core 8                 | UDP ingestion, `/api/v0/*` API, serves the SPA    |
| `HamStats.Data`   | EF Core 8 + Npgsql             | Database context and entity models                |
| `HamStats.Vue`    | Vue 3 + Vuetify 3 + Vite       | Real-time dashboard frontend                      |

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (for the Vue frontend)
- [Docker](https://www.docker.com/) (for the PostgreSQL database)

## Getting started

1. **Start the database:**

   ```bash
   docker-compose up -d
   ```

   This runs PostgreSQL 16 on `localhost:5432` (user/password/db all `hamstats`), matching the
   connection string in `HamStats.Website/appsettings.json`.

2. **Run the backend** (serves on `http://localhost:5000`, Swagger UI at `/swagger`):

   ```bash
   dotnet run --project HamStats.Website
   ```

3. **Run the frontend** (dev server on `http://localhost:3001`, proxies `/api` to the backend):

   ```bash
   cd HamStats.Vue
   npm install
   npm run dev
   ```

4. **Point N1MM+ at it:** configure N1MM+ to broadcast over UDP to this machine's IP on port
   `16000`. Contacts and scores will appear on the dashboard as they're logged.

## Configuration

- **Database connection** — `HamStats.Website/appsettings.json` (`ConnectionStrings:Default`).
- **UDP listen port** — `16000`, set in `HamStats.Website/HostedServices/N1MMWatcher.cs`.
- **Backend URL** — `http://localhost:5000`, set in `appsettings.json` (Kestrel).

## Note on data persistence

The database is **dropped and recreated on every startup** — all data is cleared each time the
backend restarts. This is intentional during early development for these ephemeral contest events.
Schema changes are made by editing the model classes directly; EF migrations will be adopted once
early development settles down.

## Building for production

```bash
dotnet publish HamStats.sln                  # backend
cd HamStats.Vue && npm run build             # frontend → HamStats.Vue/build/
```
