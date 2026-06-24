# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

HamStats is a live amateur-radio contest scoreboard. It ingests UDP broadcasts from the
[N1MM+](https://n1mmwleplus.groups.io/) contest logger, persists them to PostgreSQL, and
serves a real-time dashboard (radios, contacts, score breakdown) for events like ARRL Field Day.

## Architecture

Three components across one .NET solution (`HamStats.sln`) plus a separate Vue app:

- **HamStats.Website** — ASP.NET Core 8 Web API. Hosts the `N1MMWatcher` background service,
  exposes `/api/v0/*` read controllers, and serves the built SPA as static files. Kestrel runs on `http://localhost:5000`.
- **HamStats.Data** — EF Core 8 + Npgsql data layer. `HamStatsDbContext` and all entity models.
- **HamStats.Vue** — Vue 3 + Vuetify 3 + Vite frontend (a *separate* npm project, not referenced by the .sln). Dev server on port 3001.

### Ingestion pipeline (the core of the app)

`HamStats.Website/HostedServices/N1MMWatcher.cs` is the heart of the system. It:
1. Listens on **UDP port 16000** for N1MM+ broadcast XML.
2. Dispatches by XML root element name to a typed handler (`contactinfo`, `contactreplace`,
   `contactdelete`, `radioinfo`, `dynamicresults` → score, etc.).
3. Deserializes into the DTOs under `HamStats.Website/Data/` (these mirror N1MM's XML schema, with `[XmlElement]` field mappings).
4. Writes to Postgres via a scoped `HamStatsDbContext`.

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

### ⚠️ The database is wiped on every startup

`Program.cs` calls `EnsureDeletedAsync()` then `EnsureCreatedAsync()` on launch. There are **no
EF migrations** — the schema is recreated from the models each run, and all data is lost on
restart. This is intentional for ephemeral contest events. Schema changes take effect just by
editing the models; do not add migrations unless deliberately changing this strategy.

The plan is to switch to EF migrations once early development settles down — until then, keep
editing the models directly and rely on the wipe-and-recreate behavior.

## Commands

### Backend (.NET 8)

```bash
docker-compose up -d                              # start Postgres (required; localhost:5432, hamstats/hamstats)
dotnet build HamStats.sln
dotnet run --project HamStats.Website             # serves on http://localhost:5000, Swagger at /swagger
dotnet watch run --project HamStats.Website       # hot-reload during dev
```

### Frontend (Vue / Vite — run from `HamStats.Vue/`)

```bash
npm install
npm run dev        # dev server on http://localhost:3001, proxies /api → http://localhost:5000
npm run build      # type-checks (vue-tsc) then builds to HamStats.Vue/build/
```

There is no test suite in this repo.

### Dev workflow

Run Postgres (docker-compose) → the backend (`dotnet run`) → the Vue dev server (`npm run dev`),
then browse to `localhost:3001`. The dashboard polls `/api/v0/{contacts,radios,scores}` once per
second. The backend must be receiving N1MM UDP traffic on port 16000 to populate data.

## Conventions

- Path aliases in the Vue app (`vite.config.ts`): `@`=`src`, `@a`=`src/api`, `@c`=`src/components`, `@v`=`src/views`.
- Prettier config at repo root (`.prettierrc`): 4-space tabs, 120 print width.
- API is versioned under `/api/v0/`; controllers live in `HamStats.Website/Api/v0/Controllers/`.
