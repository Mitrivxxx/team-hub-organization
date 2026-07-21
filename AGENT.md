## Purpose
- Organization microservice for Team Hub (organizations, teams, memberships, invitations).

## Source of truth
- `team-hub-organization/` (`Program.cs`, `Configuration/`, `Controllers/`, `appsettings*.json`)
- `team-hub-organization/Data/OrganizationDbContext.cs` (EF Core models + mappings)
- `team-hub-organization/Migrations/*` (schema)
- `team-hub-organization/.env.example` (`ConnectionStrings:DefaultConnection`, `Jwt:*`, `BlobStorage:*` dev)
- `aspire/TeamHub.ServiceDefaults/Extensions.cs`
- `building-blocks/TeamHub.Observability/`
- `building-blocks/TeamHub.BlobStorage/` (dev avatar storage)

## Do
- Endpoints:
  - `GET /health` — PostgreSQL health check (`200` healthy, `503` unhealthy)
  - `GET /metrics` — Prometheus metrics
  - `POST /api/team/organizations` — create org + creator as Owner member (`201`, `409` slug conflict)
  - `GET /api/team/organizations` — list current user organizations
  - `GET /api/team/organizations/{orgId}` — organization details (member only)
  - `GET /api/team/organizations/by-slug/{slug}` — lookup by slug (member only)
  - `PATCH /api/team/organizations/{orgId}` — update `name` (slug unchanged)
  - `PUT /api/team/organizations/{orgId}/avatar` — upload avatar (`multipart/form-data`, field `file`; JPEG/PNG/WebP, max 2 MB; member-only)
  - `DELETE /api/team/organizations/{orgId}/avatar` — remove avatar (member-only)
  - `DELETE /api/team/organizations/{orgId}` — soft delete (`DeletedAt`; Owner only)
- Flow: frontend -> infrastructure nginx -> gateway `/api/team/{**catch-all}` -> this service.
- Auth: JWT Bearer (`Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`); user id from claim `sub`. For manual `dotnet run`, `Jwt__*` in `.env` must match `team-hub-auth` (same values as `Aspire:Jwt` in AppHost dev config).
- Serilog via `AddTeamHubSerilog()` — console only (no OTLP/Grafana log sink yet).
- Observability: `AddTeamHubOpenTelemetry` (traces OTLP + `/metrics`); exclude `/health` and `/metrics` from Serilog request logging.
- Keep `ExceptionMiddleware` as the first middleware (RFC 7807 `ProblemDetails`).
- Keep `CorrelationIdMiddleware` before authentication (`X-Correlation-ID` = OpenTelemetry `TraceId`; echo on response).
- Keep `UserIdLoggingMiddleware` after `UseAuthentication` / `UseAuthorization` (JWT `sub` -> `LogContext.UserId`).
- Swagger: Development only; XML summaries on controller actions.
- Dev Env: HTTP only on port `5002` (`launchSettings.json`).
- Prod Env (Docker): Host port `5002` -> container `8080`. Container `team-hub-organization-prod`.
- Docker healthcheck interval: `120s` (`docker-compose.yml` + `Dockerfile`).
- Keep this file updated after API, port, or observability changes.
- Database: PostgreSQL schema managed via EF Core migrations in `Migrations/` (auto-applied on startup).
- Connection string:
  - local dev (docker-compose.dev.yml / Aspire): `Database=organization_db`
  - docker prod compose: `Database=organizationdb`
- Postgres container `postgres-dev` (dev) and `postgres-prod` (prod) create both auth and organization databases via init script `infrastructure/postgres/init/01-create-dbs-{dev|prod}.sql` mounted at `/docker-entrypoint-initdb.d/`.
- Production-like docker compose requires copying `.env.example` to `.env` in this service directory before starting containers.
- Dev avatar storage: Azurite via `docker-compose.dev.yml` or Aspire (`BlobStorage__ConnectionString`, `BlobStorage__PublicBlobEndpoint`); `avatarUrl` in API responses is a read-only SAS URL; DB stores internal blob path.
- Avatar upload/delete returns `503` when blob storage is not configured (prod compose has no Azurite by design).

## CI (GitHub Actions)
- Workflow: `.github/workflows/ci.yml`.
- Branches: `stage` (tests only), `main` (tests + GHCR image push). `dev` has no CI.
- PRs targeting `stage` or `main` run tests before merge.
- Shared deps: dual `actions/checkout` — monorepo `Mitrivxxx/team-hub@main` then overlay this repo at `services/team-hub-organization`.
- Image: `ghcr.io/<owner>/team-hub-organization` (`latest` + short commit SHA on `main` push). Uses `GITHUB_TOKEN` (no extra secrets).

## Don't
- Do not add domain routes without updating gateway and docs.
- Do not bypass gateway for frontend API calls.
