## Repository layout

This is the self-hosted **Snap Hutao server**. The git root (`D:\projects\hutao\server`) nests two projects:

- `server/` — ASP.NET Core (net10.0) Web API. The actual C# project is at `server/server/server.csproj`; the solution is `server.slnx` at the root.
- `admin-ui/` — Vue 3 + TDesign Starter admin panel. Built output is emitted into `server/wwwroot/admin/` and served by the backend under `/admin/`.
- `tests/server.Tests/` — xUnit v3 integration suite (~340 tests). It hosts the real `Program.cs` pipeline in-process via `WebApplicationFactory`; it is **not** part of `dotnet publish` and is not deployed.

## Commands

Backend (run from `server/server`):
- Build: `dotnet build`
- Run (Development): `dotnet run` — uses `appsettings.Development.json` (local MySQL at 127.0.0.1, local Redis)
- Add migration: `dotnet ef migrations add <Name> --context AppDbContext`

Frontend (run from `admin-ui`, Node >= 22.12):
- Dev server: `npm run dev` — port 5173, proxies backend API prefixes to the production host (`vite.config.ts`)
- Build: `npm run build` — outputs to `../server/wwwroot/admin`
- API smoke test: `node scripts/api-smoke.mjs`

Tests (`dotnet test` from the repo root or `tests/server.Tests`; ~15s):
- Requires a **local MySQL** the runner can create/drop databases on. Redis is *not* needed — `HutaoServerFactory` starts an in-process Garnet server. Everything else is self-contained: fake SMTP on port 25 (MailKit's default dial), all outbound `HttpClient` traffic routed to `StubHttpMessageHandler` (unmapped URLs return 502), Sentry/OpenTelemetry/Quartz disabled.
- Overrides: `HUTAO_TEST_MYSQL_ADMIN` (admin connection, defaults to `root` on 127.0.0.1), `HUTAO_TEST_MYSQL` (test database connection, default db `snaphutao_test`), `HUTAO_TEST_SMTP_PORT`. The database is dropped and re-migrated per run, so a failed run leaves no state.
- Single class / single test: `dotnet test --filter "FullyQualifiedName~OAuthTests"`.
- Layout: `ApiSmokeTests` hits every registered route once and only asserts "no 500 + a well-formed envelope", so per-endpoint semantics live in `ClientFlowTests` (spiral abyss / role combat / gacha log sync / CDN / crash logs / license), `PassportV2Tests`, `OAuthTests`, `WebhookTests`, `ServiceTests`. Shared fixtures are under `Infrastructure/` (`HutaoServerFactory` = the collection host, `TestData` = request bodies that pass server-side validation, `StubHttpMessageHandler` = last-registered-rule-wins, `TestUsers` = seeded accounts).
- **Route coverage is enforced.** `ApiSmokeTests.EveryRegisteredRoute_IsListedInTheSmokeTables` enumerates the live endpoint table and requires every registered route to appear in `ApiSmokeTests.Endpoints` (public/client routes) or `Infrastructure.ServiceEndpoints` (`/Service/*`), and every listed URL to still resolve. Adding a controller action without a smoke row fails the suite — add the row.
- The test host needs `App/RSAPrivateKey`; the project links the local `server/privkey.key` into its output. Test-only config (OAuth AES key, GitHub client id/secret, SMTP credentials) is set inside `HutaoServerFactory.ConfigureWebHost`.

## Architecture

**Two EF Core DbContexts.** `AppDbContext` is the primary DB and owns all migrations — always pass `--context AppDbContext`. `MetadataDbContext` has no migrations. Both use Pomelo MySQL and are pooled.

**Migrations are generated offline.** The production MySQL is not reachable from the dev/build environment, so `DesignTimeAppDbContextFactory` pins `MySqlServerVersion(8,0,32)` instead of `ServerVersion.AutoDetect` (runtime `Program.cs` still AutoDetects the real server). Migrations **auto-apply only in RELEASE builds** at startup (`Program.MigrateDatabase`, `[Conditional("RELEASE")]`) — there is no manual `database update` step. After generating a migration, open the `Up()`/`Down()` and confirm it contains only the intended schema ops before deploying.

**Response envelope.** API responses use `Models/Response/Response.cs` with a `retcode` (`ReturnCode` enum), `message` (English), and optional `data`/`l10nKey`. Use `Response.Success(...)` / `Response.Fail(code, msg, key)` / `Response<T>.Success(...)` — static messages must pass an `l10nKey` from `server/ServerKeys.cs`; messages with runtime interpolation stay English without a key. The frontend axios client (`admin-ui/src/api/request.js`) unwraps this envelope, auto-refreshes tokens on `514002`, treats non-envelope bodies as pass-through third-party data, and rewrites `message` through `l10nKey` (namespace `pages.server.*`).

**Auth.** JWT bearer (`Program.cs`) plus ASP.NET Identity (`HutaoUser`). Get the current user with the `GetUserId()` / `TryGetUserId()` extensions (`Controllers/UserAuthorizationExtension.cs`), which read the `PassportClaimTypes.UserId` claim. Login is via OAuth (GitHub) with AES-encrypted state, plus passport/password flows.

**Maintainer-gated admin surface.** Admin/ops endpoints live under `ServiceController` (`/Service/*`) and are gated by `[Authorize]` + the `ValidateMaintainPermission` action filter (checks `IsMaintainer`, cached 15 min). The admin panel is the intended primary operations surface (it replaced older email flows) — when adding a server capability, default to also exposing it as a maintainer-gated `/Service/*` endpoint and wiring it into the panel.

**Background jobs.** Quartz schedules statistics-refresh and record-clean jobs (`Job/`), registered with cron expressions in `Program.cs`.

**Observability.** Sentry + OpenTelemetry (traces + logs) export to a self-hosted Sentry OTLP endpoint. Redis is the distributed cache (`DistributedCacheExtension` adds a `GetOrCreateAsync` helper).

## Conventions

- `ImplicitUsings` is **disabled**; common usings are centralized in `server/GlobalUsing.cs`. Add shared namespaces there rather than per-file.
- `Nullable` is enabled. `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes` is on, so non-nullable reference types are **not** treated as `[Required]`.
- JSON uses original property names (no camelCase policy), ignores nulls when writing, and is case-insensitive on read.
- Code comments remain Chinese; API response messages are English + `l10nKey` (see Response envelope above). Client-side translations live in the launcher's `SH.resx`/`SH.en.resx` and in `admin-ui/src/locales/lang/<locale>/pages/server.json` — after adding/changing keys in `ServerKeys.cs`, update the launcher resx and rerun `node scripts/extract-server-l10n.mjs` (from `admin-ui/`) to regenerate the admin panel's copy from the launcher resx.
- Frontend uses TDesign Vue Next components (keep the official TDesign starter look). Two API layers coexist: the starter's TS client (`@/utils/request`) and a purpose-built JS axios client (`@/api/request.js` + `@/api/service.js`) for the backend envelope — new backend calls go through the latter. Page UI strings are i18n'd via per-page locale fragments (`src/locales/lang/<locale>/pages/<page>.json`, use `$t` in templates / `t` from `@/locales` in scripts).

## Deployment note

A no-dependency change only needs `server.dll` replaced. Any newly added NuGet package must ship its DLLs alongside — a missing dependency DLL will crash startup.
