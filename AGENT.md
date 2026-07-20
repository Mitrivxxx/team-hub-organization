## Purpose
- Organization microservice skeleton (Web API) for Team Hub.

## Source of truth
- `team-hub-organization/` (`Program.cs`, `Configuration/`, `Controllers/`, `appsettings*.json`)
- `team-hub-organization/Data/OrganizationDbContext.cs` (EF Core models + mappings)
- `team-hub-organization/Migrations/*` (schema)
- `team-hub-organization/.env.example` (`ConnectionStrings:DefaultConnection`)
- `aspire/TeamHub.ServiceDefaults/Extensions.cs`
- `building-blocks/TeamHub.Observability/`

## Do
- Endpoints: `GET /health`, `GET /metrics` (Prometheus). Controllers folder ready for domain APIs.
- Flow: frontend -> infrastructure nginx -> gateway `/api/team/{**catch-all}` -> this service.
- Serilog via `AddTeamHubSerilog()` — console only (no OTLP/Grafana log sink yet).
- Observability: `AddTeamHubOpenTelemetry` (traces OTLP + `/metrics`); exclude `/health` and `/metrics` from Serilog request logging.
- Keep `CorrelationIdMiddleware` before request logging (`X-Correlation-ID` = OpenTelemetry `TraceId`; echo on response).
- Dev Env: HTTP only on port `5002` (`launchSettings.json`).
- Prod Env (Docker): Host port `5002` -> container `8080`. Container `team-hub-organization-prod`.
- Docker healthcheck interval: `120s` (`docker-compose.yml` + `Dockerfile`).
- Keep this file updated after API, port, or observability changes.
- Database: PostgreSQL schema managed via EF Core migrations in `Migrations/` (auto-applied on startup).
- Connection string:
  - local dev (docker-compose.dev.yml / Aspire): `Database=organization_db`
  - docker prod compose: `Database=organizationdb`
- Production-like docker compose requires copying `.env.example` to `.env` in this service directory before starting containers.

## CI (GitHub Actions)
- Workflow: `.github/workflows/ci.yml`.
- Branches: `stage` (tests only), `main` (tests + GHCR image push). `dev` has no CI.
- PRs targeting `stage` or `main` run tests before merge.
- Shared deps: dual `actions/checkout` — monorepo `Mitrivxxx/team-hub@main` then overlay this repo at `services/team-hub-organization`.
- Image: `ghcr.io/<owner>/team-hub-organization` (`latest` + short commit SHA on `main` push). Uses `GITHUB_TOKEN` (no extra secrets).

## Don't
- Do not add domain routes without updating gateway and docs.
- Do not bypass gateway for frontend API calls.
