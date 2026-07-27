## Purpose
- Organization microservice for Team Hub (organizations, teams, memberships, roles/permissions, invitations).

## Source of truth
- `team-hub-organization/` (`Program.cs`, `Configuration/`, `Controllers/`, `appsettings*.json`)
- `team-hub-organization/Data/OrganizationDbContext.cs` (EF Core models + mappings)
- `team-hub-organization/Migrations/*` (schema)
- `team-hub-organization/.env.example` (`ConnectionStrings:DefaultConnection`, `Jwt:*`, `BlobStorage:*` dev)
- `docs/organization.mb` (API contract)
- `aspire/TeamHub.ServiceDefaults/Extensions.cs`
- `building-blocks/TeamHub.Observability/`
- `building-blocks/TeamHub.BlobStorage/` (dev avatar storage)

## Code layout (Members)
- Controllers / Models / Services mirror manage UI tabs under `Members/`:
  - `AllMembers` — org members CRUD + multi-role assignment + member teams
  - `Invitations` — org invitations + token accept/reject + `/me/invitations`
  - `Roles` — org/team role CRUD + role↔permission attach + role members
  - `Permissions` — org-scoped permission CRUD (`/{orgId}/permissions`)
  - `Activity` — placeholder (empty)
  - `ImportExport` — placeholder (empty)
- Models keep namespace `team_hub_organization.Models` (EF migrations stable); folders only.
- Outside Members: `Controllers/Organizations*`, `Controllers/TeamsController`, `Controllers/Me`, `Services/Organizations`, `Services/Teams`, `Services/Me`, `Services/Rbac`.

## Do
- Endpoints (base `/api/organizations/v0.1.0`, JWT required):
  - `GET /health` — PostgreSQL health check (`200` healthy, `503` unhealthy)
  - `GET /metrics` — Prometheus metrics
  - Organization: `POST /`, `GET /`, `GET /{orgId}`, `GET /by-slug/{slug}`, `PATCH /{orgId}`, `PUT|DELETE /{orgId}/avatar`, `DELETE /{orgId}`, `POST /{orgId}/transfer-ownership`, `POST /{orgId}/leave`
  - Members: `GET|POST /{orgId}/members` (`GET` optional `?roleId=&teamId=`), `GET|PATCH|DELETE /{orgId}/members/{userId}`, `GET /{orgId}/members/{userId}/teams` — body uses `roleIds[]`
- Internal gRPC (not via gateway): `OrganizationMemberService.ListMembers` on port `5102` (dev) / `8081` (docker); reuses `IMemberService.ListAsync`.
- Kestrel: REST/health on `8080` (Http1AndHttp2), gRPC on `8081` (Http2 only).
- Shared contracts: `building-blocks/TeamHub.GrpcContracts` (`Protos/organization/v1/members.proto`).
- User profiles (name/surname) stay in auth; BFF GraphQL composes them for the All Members UI.
  - Teams: CRUD under `/{orgId}/teams`, avatar, team members CRUD
  - Roles: CRUD `/{orgId}/roles`, permission attach/replace/remove by `permissionId`, role members assign/list/revoke
  - Permissions: CRUD `/{orgId}/permissions` (org-scoped; system codes cloned on org create)
  - Invitations: org-scoped list/create/get/cancel/resend (`orgRoleIds[]`); `invitations/by-token/{token}` get/accept/reject
  - Me: `GET /{orgId}/me` (roles union + permissions), `GET /me/invitations`
- See `docs/organization.mb` for request bodies, status codes, authZ rules, and future domain event contracts.
- RBAC: org-scoped permissions; org-scoped roles (`ORG` | `TEAM`); members assigned via `organization_member_roles` (many-to-many). Effective permissions = union of roles.
- System roles on org create: Owner, Admin, Member (org) + TeamLead, Member (team). Owner gets all permissions; Admin all except `org.delete`.
- API versioning: URL path `/api/organizations/v0.1.0/*` (SemVer `0.1.0`; Asp.Versioning major.minor `0.1`, packages `Asp.Versioning.Mvc` / `ApiExplorer` 8.1.0).
- Flow: frontend -> infrastructure nginx -> gateway `/api/organizations/{**catch-all}` -> this service.
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
- Database: PostgreSQL schema managed via EF Core migrations in `Migrations/` (auto-applied on startup). System permission templates cloned per org on create (no global catalog table).
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
- Do not store user profiles (auth owns users).
