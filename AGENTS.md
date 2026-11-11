## Repository layout

This is the self-hosted **Snap Hutao server**. The git root (`D:\projects\hutao\server`) nests two projects:

- `server/` — ASP.NET Core (net10.0) Web API. The actual C# project is at `server/server/server.csproj`; the solution is `server.slnx` at the root.
- `admin-web/` — React 18 + Semi Design admin panel (real path routing, no hash). Built output is emitted into `server/wwwroot/admin/` and served by the backend under `/admin/`.
- `tests/server.Tests/` — xUnit v3 integration suite (~340 tests). It hosts the real `Program.cs` pipeline in-process via `WebApplicationFactory`; it is **not** part of `dotnet publish` and is not deployed.

## Commands

Backend (run from `server/server`):
- Build: `dotnet build`
- Run (Development): `dotnet run` — uses `appsettings.Development.json` (local MySQL at 127.0.0.1, local Redis)
- Add migration: `dotnet ef migrations add <Name> --context AppDbContext`

Frontend (run from `admin-web`, Node >= 22.12, package manager is **pnpm** — CI installs with `pnpm install --frozen-lockfile`):
- Dev server: `pnpm dev` — port 5174, `/api` proxied to the local backend (`https://localhost:7013`, override with `VITE_DEV_API_TARGET`)
- Build: `pnpm build` — typecheck then output to `../server/wwwroot/admin`
- Typecheck only: `pnpm typecheck`
- Sentry: the panel reports to the self-hosted instance (`VITE_SENTRY_DSN` in `.env`, cleared in `.env.development`). The build emits **hidden** sourcemaps and, when `SENTRY_AUTH_TOKEN` + `SENTRY_ORG` + `SENTRY_PROJECT` are all set in the environment, `@sentry/vite-plugin` uploads them and deletes them from the output — without those three the build skips the upload and leaves the maps on disk. Both sides of that path use one release id: `vite.config.ts` computes `admin-web@<version>+<commit>` (the server's own release is `server@vX.Y.Z` from `Program.applicationVersion`), passes it to the plugin, and injects it as `__SENTRY_RELEASE__` for `Sentry.init` — keep those in sync or uploaded maps won't resolve.

Tests (~15s). `dotnet test` does **not** work on SDK 10.0.4xx: xunit.v3 ships Microsoft.Testing.Platform, which refuses the VSTest host (`Testing with VSTest target is no longer supported`). Build, then run the test binary directly:
`dotnet build tests/server.Tests/server.Tests.csproj -c Debug && ./tests/server.Tests/bin/Debug/net10.0/server.Tests.exe`
- Requires a **local MySQL** the runner can create/drop databases on. Redis is *not* needed — `HutaoServerFactory` starts an in-process Garnet server. Everything else is self-contained: fake SMTP on port 25 (MailKit's default dial), all outbound `HttpClient` traffic routed to `StubHttpMessageHandler` (unmapped URLs return 502), Sentry/OpenTelemetry/Quartz disabled.
- Overrides: `HUTAO_TEST_MYSQL_ADMIN` (admin connection, defaults to `root` on 127.0.0.1), `HUTAO_TEST_MYSQL` (test database connection, default db `snaphutao_test`), `HUTAO_TEST_SMTP_PORT`. The database is dropped and re-migrated per run, so a failed run leaves no state.
- CI runs the suite in the `test` job of `.github/workflows/build.yml` against a `mysql:8.0` service container, and `package` is gated on it (`needs: test`). Two container-specific gotchas are handled in that job: `ResetDatabaseAsync` only creates `@localhost` accounts while a container connection arrives from the docker gateway, so the job pre-creates `'snaphutao_test'@'%'` plus a dummy `'snaphutao_server'@'localhost'` (MySQL 8 no longer auto-creates grantees), and the fake SMTP binds port 25, so the job sets `net.ipv4.ip_unprivileged_port_start=0`.
- Single class / single test: `-class server.Tests.OAuthTests` / `-method server.Tests.OAuthTests.<Name>` (the runner's `--help` lists the rest).
- Layout: `ApiSmokeTests` hits every registered route once and only asserts "no 500 + a well-formed envelope", so per-endpoint semantics live in `ClientFlowTests` (spiral abyss / role combat / gacha log sync / CDN / crash logs / license / client version tracking), `PassportV2Tests`, `OAuthTests`, `WebhookTests`, `ServiceTests`. Shared fixtures are under `Infrastructure/` (`HutaoServerFactory` = the collection host, `TestData` = request bodies that pass server-side validation, `StubHttpMessageHandler` = last-registered-rule-wins, `TestUsers` = seeded accounts).
- **Route coverage is enforced.** `ApiSmokeTests.EveryRegisteredRoute_IsListedInTheSmokeTables` enumerates the live endpoint table and requires every registered route to appear in `ApiSmokeTests.Endpoints` (public/client routes) or `Infrastructure.ServiceEndpoints` (`/Service/*`), and every listed URL to still resolve. Adding a controller action without a smoke row fails the suite — add the row.
- The test host needs `App/RSAPrivateKey`; the project links the local `server/privkey.key` into its output. Test-only config (OAuth AES key, GitHub client id/secret, SMTP credentials) is set inside `HutaoServerFactory.ConfigureWebHost`.

## Architecture

**Two EF Core DbContexts.** `AppDbContext` is the primary DB and owns all migrations — always pass `--context AppDbContext`. `MetadataDbContext` has no migrations. Both use Pomelo MySQL and are pooled. `MetadataDbContext` currently covers only `yae_achievement_metadata` (feeding `/api/client/features/AchievementFieldId_{version}`), `known_items` and `gacha_events`; the metadata-to-DB feature (`metadata_document` / `metadata_object` / i18n / translation / projection + its `/metadata/*` controller, admin panel page and import tool) was deliberately removed — clients keep reading metadata from the original path, and the code is recoverable from git history if that work resumes.

**Migrations are generated offline.** The production MySQL is not reachable from the dev/build environment, so `DesignTimeAppDbContextFactory` pins `MySqlServerVersion(8,0,32)` instead of `ServerVersion.AutoDetect` (runtime `Program.cs` still AutoDetects the real server). Migrations **auto-apply only in RELEASE builds** at startup (`Program.MigrateDatabase`, `[Conditional("RELEASE")]`) — there is no manual `database update` step. After generating a migration, open the `Up()`/`Down()` and confirm it contains only the intended schema ops before deploying.

**Response envelope.** API responses use `Models/Response/Response.cs` with a `retcode` (`ReturnCode` enum), `message` (English), and optional `data`/`l10nKey`. Use `Response.Success(...)` / `Response.Fail(code, msg, key)` / `Response<T>.Success(...)` — static messages must pass an `l10nKey` from `server/ServerKeys.cs`; messages with runtime interpolation stay English without a key. The frontend axios client (`admin-web/src/lib/http.ts`) unwraps this envelope, auto-refreshes tokens on `514002`, treats non-envelope bodies as pass-through third-party data, and rewrites `message` through `l10nKey` (namespace `server.*`).

**Routing conventions (2026-10-30).** Internal APIs are REST-shaped: `/api/<resource>` for launcher/public resources and `/api/admin/<resource>` for admin-only ones; use the verb that matches the operation (GET read, POST create/action, PUT/PATCH update, DELETE remove) and keep verbs, `Get`, `Update`, `List` and `SetEnabled` out of path segments. Route parameters carry ids (`/users/{userId}`, `/bans/{uid}`); list filters stay in the query. Only external contracts are exempt: `/patch/hutao` (forced updates) and the third-party callbacks `/api/OAuth/Callback/GitHub`, `/Admin/Sso/Callback`, `/Passport/Sso/Callback`, `/api/Webhook/Incoming/Afdian`. The launcher and admin panel are updated in the same change; do not add legacy route aliases.

**Auth.** JWT bearer (`Program.cs`) plus ASP.NET Identity (`HutaoUser`). Get the current user with the `GetUserId()` / `TryGetUserId()` extensions (`Controllers/UserAuthorizationExtension.cs`), which read the `PassportClaimTypes.UserId` claim. Login is via OAuth (GitHub) with AES-encrypted state, plus passport/password flows.

**Maintainer-gated admin surface.** Admin/ops endpoints live under `ServiceController` (`/Service/*`) and are gated by `[Authorize]` + the `ValidateMaintainPermission` action filter (checks `IsMaintainer`, cached 15 min). The admin panel is the intended primary operations surface (it replaced older email flows) — when adding a server capability, default to also exposing it as a maintainer-gated `/Service/*` endpoint and wiring it into the panel.

**Background jobs.** Quartz schedules statistics-refresh and record-clean jobs (`Job/`), registered with cron expressions in `Program.cs`.

**Observability.** Sentry + OpenTelemetry (traces + logs) export to a self-hosted Sentry OTLP endpoint. Redis is the distributed cache (`DistributedCacheExtension` adds a `GetOrCreateAsync` helper).

## Conventions

- `ImplicitUsings` is **disabled**; common usings are centralized in `server/GlobalUsing.cs`. Add shared namespaces there rather than per-file.
- `Nullable` is enabled. `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes` is on, so non-nullable reference types are **not** treated as `[Required]`.
- JSON uses original property names (no camelCase policy), ignores nulls when writing, and is case-insensitive on read.
- Code comments remain Chinese; API response messages are English + `l10nKey` (see Response envelope above). Client-side translations live in the launcher's `SH.resx`/`SH.en.resx` and in `admin-web/src/i18n/<locale>/server.json` — after adding/changing keys in `ServerKeys.cs`, update the launcher resx and rerun `node scripts/extract-server-l10n.mjs` (from `admin-web/`) to regenerate the admin panel's copy from the launcher resx. The generator requires each `ServerKeys` constant name to equal its value, and `ServerKeyLocalizationTests` asserts both locales cover every key with no leftovers.
- Frontend uses Semi Design components (`@douyinfe/semi-ui`) with Tailwind utilities for layout; the dark theme is Semi's `theme-mode="dark"` attribute on `<body>` plus a `dark` class on `<html>` (see `src/lib/theme.ts`). Backend calls go through the single axios client `@/lib/http`. Page UI strings are i18n'd via per-page fragments (`src/i18n/<locale>/<page>.json`, each file carrying its own top-level namespace; use `t`/`te` from `@/i18n`).
- **DI lifetime markers are a bare keyword.** A class annotated with its lifetime gets exactly `// Singleton`, `// Scoped` or `// Transient` on the line above the type — no `：` explanation, no parenthetical rationale. If the "why" matters (e.g. it depends on a scoped service), write it next to the registration in `Program.cs` or in the commit message, not on the marker.

## Code style (StyleCop)

**Style is enforced by StyleCop.Analyzers repo-wide, not per-diff.** When you edit a file, bring the whole file to the standard — do not leave violations in the lines you happened to touch or in surrounding code, and do not narrow the fix to the feature you were asked for. New code must be clean on first build.

- `server/server.csproj` references `StyleCop.Analyzers.Unstable` (build only, `PrivateAssets=all`) and has `TreatWarningsAsErrors=true`, so an enabled rule violation is a **build error**, not a warning. `stylecop.json` is an `AdditionalFile` (zh-CN docs, `using` outside namespace, `System`-not-first, consecutive usings allowed, 4-space indent).
- **All configuration lives in `server/.editorconfig`, not the repo-root one.** `server/.editorconfig` contains `root = true`, which stops Roslyn's config walk before it reaches `../.editorconfig`; anything added to the root file is silently ignored for every source file.
- Rules are **enabled by default** in StyleCop 1.2.x, so the file only lists `dotnet_diagnostic.SAxxxx.severity = none` overrides, each with a reason. The disabled ones protect deliberate house conventions: no `this.` (SA1101), `_camelCase` private fields/consts (SA1309/SA1303/SA1313), root namespace `server` (SA1300), `System`-not-first usings (SA1210), no copyright header (SA1633/1636/1641), multiple types per file and file name ≠ type name (SA1402/SA1649), `#region` (SA1124), optional member docs (SA1600–1603) and `Gets`/`Sets` phrasing (SA1623), closing `)` on its own line (SA1009), feature-grouped members rather than kind ordering (SA1201/1202/1204), incomplete `<param>` lists (SA1611/1615 — the compiler's CS1573 still catches partial ones), and Chinese `。` endings (SA1629/1642).
- EF Core migrations are excluded by a `[Migrations/*.cs]` section. The pattern must be relative — `[**/Migrations/*.cs]` silently matches nothing.
- Enabled rules are the ones the house style already satisfies: blank line between members (SA1516), comment spacing (`// x`, SA1005), mandatory braces (SA1503), trailing commas in initializers (SA1413), doc tag correctness (SA1612/SA1614/SA1616), using placement (SA1208/1209 aside from `System`), and general spacing/layout.
- To fix: `dotnet build server/server.csproj --no-incremental` lists violations with `file(line,col)`; `dotnet format analyzers server/server.csproj --diagnostics=SA1516,SA1005,...` auto-fixes the ones that ship fixers (plain `dotnet format <project>` does **not** apply analyzer fixes, and the whitespace/imports fixers do run — hence `end_of_line = crlf` / `insert_final_newline = true` must stay aligned with the working tree).
- Do **not** add `<EditorConfigFiles Include="...">` to the csproj: the SDK already passes ancestor `.editorconfig` files to `csc`, and a manual include produces `CS8700 Multiple analyzer config files cannot be in the same directory`.
- Documentation analysis only runs because `GenerateDocumentationFile=true` (with `<NoWarn>$(NoWarn);CS1591</NoWarn>`); it also turns on compiler doc checks, so doc blocks must sit **above** attributes such as `[Authorize]` (CS1587) and partly documented signatures are errors (CS1573).

## Deployment note

A no-dependency change only needs `server.dll` replaced. Any newly added NuGet package must ship its DLLs alongside — a missing dependency DLL will crash startup.
