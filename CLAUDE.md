# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

HamStats is a live amateur-radio contest scoreboard. It ingests UDP broadcasts from the
[N1MM+](https://n1mmwleplus.groups.io/) contest logger, persists them to a SQLite database, and
serves a real-time dashboard (radios, contacts, score breakdown) for events like ARRL Field Day.

## Architecture

Three components across one .NET solution (`HamStats.sln`) plus a separate Vue app:

- **HamStats.Website** — ASP.NET Core 10 Web API. Hosts the `N1MMWatcher` background service,
  exposes `/api/v0/*` read controllers, and serves the built SPA as static files. Kestrel runs on `http://localhost:5000`.
- **HamStats.Data** — EF Core 10 + SQLite data layer. `HamStatsDbContext` and all entity models.
- **HamStats.Vue** — Vue 3 + Vuetify 3 + Vite frontend (a *separate* npm project, not referenced by the .sln). Dev server on port 3001.

### Ingestion pipeline (the core of the app)

`HamStats.Website/HostedServices/N1MMWatcher.cs` is the heart of the system. It:
1. Listens on **UDP port 12060** (N1MM+ default; override with `N1MM:BroadcastPort`) for N1MM+ broadcast XML.
2. Dispatches by XML root element name to a typed handler (`contactinfo`, `contactreplace`,
   `contactdelete`, `radioinfo`, `dynamicresults` → score, etc.).
3. Deserializes into the DTOs under `HamStats.Website/Data/` (these mirror N1MM's XML schema, with `[XmlElement]` field mappings).
4. Writes to SQLite via a scoped `HamStatsDbContext`.

To add support for a new N1MM message type: add an XML DTO in `HamStats.Website/Data/`, add a
`case` in `HandleMessage`, add the `ProcessXml<T>` branch, and write a `Process(T)` overload.

### Data model

Two parallel contact tables, related one-to-one:
- **N1MMContact** — a near-verbatim mirror of the N1MM message (raw audit copy).
- **Contact** — the normalized record linked to a `Radio`, used by the API/dashboard.

`Process(ContactInfo)` creates both at once; `Process(ContactReplace)` must update both copies in lockstep.
Other entities: `Radio` → `VFO` → `N1MMRadio` (a radio has VFOs; each VFO maps to an N1MM radio
slot by `RadioNumber`, 1=A / 2=B). `Score` has many `ScoreBreakdown` (per band/mode).

EF entity configuration lives **on the model classes themselves** — each model implements
`IEntityTypeConfiguration<T>` and is wired up via `ApplyConfigurationsFromAssembly` in `HamStatsDbContext.OnModelCreating`.

### The database persists across restarts (EF migrations)

`Program.cs` calls `MigrateAsync()` on launch, applying any pending EF migrations to `hamstats.db`.
The database is **durable, long-lived storage** — it is no longer wiped on startup.

Migrations live in `HamStats.Data/Migrations/` (the assembly with `HamStatsDbContext`). After
editing a model, scaffold a migration so the change takes effect:

```bash
dotnet ef migrations add <Name> --project HamStats.Data --startup-project HamStats.Website
```

(`Microsoft.EntityFrameworkCore.Design` is referenced by both projects so the tools resolve the
DbContext.) The next startup applies it automatically. Design for durability — do **not** architect
around losing data on restart (e.g. don't push data into separate files or external stores just to
survive a restart, and don't shy away from storing data that's expensive to rebuild).

`Setting` (key/value) follows this same model — a normal table holding app settings like the time
zone, persisted like everything else.

### Live log streaming & runtime log level

The dashboard has a live "Packet Log" console fed by [RoushTech.Asio](https://www.nuget.org/packages/RoushTech.Asio.SignalR/)
(the app-log tail — *not* the job-session feature, so no Redis). `AddAsioAppLogSignalR()` captures
all `ILogger` output into a ring buffer and streams it over SignalR at **`/hubs/app-log`** (mapped
with `requireAuthorization: false` — HamStats has no auth). The client event is `AppLog` (`AppLogEntry`).

- The N1MM watcher logs **raw packets at `Trace`** and per-message summaries at `Debug`.
- ⚠️ The app-log provider's alias is **`AsioAppLog`** (the `Asio` alias belongs to the unused
  job-session provider). So its capture level is controlled by the **`Logging:AsioAppLog`** config
  section — using `Logging:Asio` silently does nothing.
- `LogLevelController` (`/api/v0/loglevel`) changes that level **at runtime**: it writes to an
  in-memory `RuntimeLogLevelProvider` config source whose `OnReload()` makes the logging filters
  recompute live. The UI's capture-level dropdown drives this.

### Settings & time zones

`SettingsController` (`/api/v0/settings`) stores a time zone in the `Setting` table; `/timezones`
returns the supported US IANA zones. **All times in the UI are rendered in this zone, not UTC** —
conversion happens client-side (`HamStats.Vue/src/timezone.ts`) using `Intl.DateTimeFormat`.
For that to be correct the API must emit UTC timestamps with a trailing `Z`; SQLite drops
`DateTimeKind` on read, so `HamStatsDbContext.OnModelCreating` applies a global value converter that
re-marks every `DateTime` as `Utc`.

### Intended use: passive kiosk display

HamStats is meant to run as a **passive wall/kiosk display** at the event — shown to everyone, with
**no mouse or keyboard** during operation. Design with that in mind: the dashboard should sit and
update on its own and be able to **auto-cycle through views** (radios / contacts / scores / etc.)
without interaction. Occasional admin actions (settings, packet log) live behind the app-bar menu
as dialogs so they stay out of the way of the passive display.

## Commands

> **Do NOT start the app yourself (no `dotnet run`/`dotnet watch`/`npm run dev`).** The developer
> keeps the backend (`:5000`) and Vue dev server (`:3001`) running; launching your own instance
> steals those ports and kills their hot-reload. To verify changes, **build only** (`dotnet build`,
> `npm run build`/`vue-tsc --noEmit`) and ask the developer to exercise the running app, or have
> them confirm. Only run the app if explicitly asked to.

### Backend (.NET 10)

```bash
dotnet build HamStats.sln
dotnet run --project HamStats.Website             # serves on http://localhost:5000, OpenAPI doc at /openapi/v1.json
dotnet watch run --project HamStats.Website       # hot-reload during dev
```

No database server is needed for dev — the app creates a local `hamstats.db` SQLite file on
startup and applies migrations to it (see above); it persists across runs. `docker-compose.yml` is
purely the shippable artifact (it runs the published image), not a dev dependency.

### Frontend (Vue / Vite — run from `HamStats.Vue/`)

```bash
npm install
npm run dev        # dev server on http://localhost:3001, proxies /api → http://localhost:5000
npm run build      # type-checks (vue-tsc) then builds to HamStats.Vue/build/
```

There is no test suite in this repo.

### Docker (shippable image)

`Dockerfile` is a 3-stage build (Vue build → .NET publish → `aspnet:10.0` runtime) producing
`roushtech/hamstats`, matching the DireControl pattern. The Vue app builds to `build/` (not `dist/`)
and is copied to `wwwroot`. Note: `appsettings.json` pins Kestrel to `localhost:5000`, which is
unreachable inside a container — the Dockerfile overrides it with
`Kestrel__Endpoints__Http__Url=http://+:5000`. The container exposes TCP 5000 (HTTP) and UDP 12060
(N1MM), and stores persistent state under `/data` (mounted as a volume, like DireControl): the
SQLite DB at `/data/hamstats.db` (`ConnectionStrings__Default`) and the downloaded callsign/postal
dumps at `/data/callsign-cache` (`CallsignLookup__CacheDirectory`). docker-compose mounts `./data:/data`.

```bash
docker build -t hamstats:local --build-arg version=0.0.1 --build-arg gitsha=$(git rev-parse HEAD) .
```

### Dev workflow

Run the backend (`dotnet run`) → the Vue dev server (`npm run dev`), then browse to
`localhost:3001`. The dashboard polls `/api/v0/{contacts,radios,scores}` once per second. The
backend must be receiving N1MM UDP traffic on port 12060 to populate data.

## Conventions

- Path aliases in the Vue app (`vite.config.ts`): `@`=`src`, `@a`=`src/api`, `@c`=`src/components`, `@v`=`src/views`.
- Prettier config at repo root (`.prettierrc`): 4-space tabs, 120 print width.
- API is versioned under `/api/v0/`; controllers live in `HamStats.Website/Api/v0/Controllers/`.
- Keep comments extremely concise — one line where possible, only for non-obvious *why*, never restating the code.
