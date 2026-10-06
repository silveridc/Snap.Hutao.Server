## Repository layout

This is the self-hosted **Snap Hutao server**. The git root (`D:\projects\hutao\server`) nests two projects:

- `server/` — ASP.NET Core (net10.0) Web API. The actual C# project is at `server/server/server.csproj`; the solution is `server.slnx` at the root.
- `admin-ui/` — Vue 3 + TDesign Starter admin panel. Built output is emitted into `server/wwwroot/admin/` and served by the backend under `/admin/`.

## Commands

Backend (run from `server/server`):
- Build: `dotnet build`
- Run (Development): `dotnet run` — uses `appsettings.Development.json` (local MySQL at 127.0.0.1, local Redis)
- Add migration: `dotnet ef migrations add <Name> --context AppDbContext`

Frontend (run from `admin-ui`, Node >= 22.12):
- Dev server: `npm run dev` — port 5173, proxies backend API prefixes to the production host (`vite.config.ts`)
- Build: `npm run build` — outputs to `../server/wwwroot/admin`
- API smoke test: `node scripts/api-smoke.mjs`

## Architecture

**Two EF Core DbContexts.** `AppDbContext` is the primary DB and owns all migrations — always pass `--context AppDbContext`. `MetadataDbContext` has no migrations. Both use Pomelo MySQL and are pooled.

**Migrations are generated offline.** The production MySQL is not reachable from the dev/build environment, so `DesignTimeAppDbContextFactory` pins `MySqlServerVersion(8,0,32)` instead of `ServerVersion.AutoDetect` (runtime `Program.cs` still AutoDetects the real server). Migrations **auto-apply only in RELEASE builds** at startup (`Program.MigrateDatabase`, `[Conditional("RELEASE")]`) — there is no manual `database update` step. After generating a migration, open the `Up()`/`Down()` and confirm it contains only the intended schema ops before deploying.

**Response envelope.** API responses use `Models/Response/Response.cs` with a `retcode` (`ReturnCode` enum), `message`, and optional `data`/`l10nKey`. Use `Response.Success(...)` / `Response.Fail(code, msg)` / `Response<T>.Success(...)`. `ReturnCode.Success = 0`; failures are 5xxxxx codes. The frontend axios client (`admin-ui/src/api/request.js`) unwraps this envelope, auto-refreshes tokens on `514002`, and treats non-envelope bodies as pass-through third-party data.

**Auth.** JWT bearer (`Program.cs`) plus ASP.NET Identity (`HutaoUser`). Get the current user with the `GetUserId()` / `TryGetUserId()` extensions (`Controllers/UserAuthorizationExtension.cs`), which read the `PassportClaimTypes.UserId` claim. Login is via OAuth (GitHub) with AES-encrypted state, plus passport/password flows.

**Maintainer-gated admin surface.** Admin/ops endpoints live under `ServiceController` (`/Service/*`) and are gated by `[Authorize]` + the `ValidateMaintainPermission` action filter (checks `IsMaintainer`, cached 15 min). The admin panel is the intended primary operations surface (it replaced older email flows) — when adding a server capability, default to also exposing it as a maintainer-gated `/Service/*` endpoint and wiring it into the panel.

**Background jobs.** Quartz schedules statistics-refresh and record-clean jobs (`Job/`), registered with cron expressions in `Program.cs`.

**Observability.** Sentry + OpenTelemetry (traces + logs) export to a self-hosted Sentry OTLP endpoint. Redis is the distributed cache (`DistributedCacheExtension` adds a `GetOrCreateAsync` helper).

## Conventions

- `ImplicitUsings` is **disabled**; common usings are centralized in `server/GlobalUsing.cs`. Add shared namespaces there rather than per-file.
- `Nullable` is enabled. `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes` is on, so non-nullable reference types are **not** treated as `[Required]`.
- JSON uses original property names (no camelCase policy), ignores nulls when writing, and is case-insensitive on read.
- Comments and user-facing messages are largely in Chinese; match the surrounding style.
- Frontend uses TDesign Vue Next components (keep the official TDesign starter look). Two API layers coexist: the starter's TS client (`@/utils/request`) and a purpose-built JS axios client (`@/api/request.js` + `@/api/service.js`) for the backend envelope — new backend calls go through the latter.

## Deployment note

A no-dependency change only needs `server.dll` replaced. Any newly added NuGet package must ship its DLLs alongside — a missing dependency DLL will crash startup.
