FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY aspire/TeamHub.ServiceDefaults/TeamHub.ServiceDefaults.csproj aspire/TeamHub.ServiceDefaults/
COPY building-blocks/TeamHub.Observability/TeamHub.Observability.csproj building-blocks/TeamHub.Observability/
COPY services/team-hub-organization/team-hub-team/team-hub-team.csproj services/team-hub-organization/team-hub-team/
RUN dotnet restore services/team-hub-organization/team-hub-team/team-hub-team.csproj

COPY aspire/TeamHub.ServiceDefaults/ aspire/TeamHub.ServiceDefaults/
COPY building-blocks/TeamHub.Observability/ building-blocks/TeamHub.Observability/
COPY services/team-hub-organization/team-hub-team/ services/team-hub-organization/team-hub-team/
RUN dotnet publish services/team-hub-organization/team-hub-team/team-hub-team.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish .
RUN chown -R app:app /app

USER app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=120s --timeout=5s --start-period=15s --retries=5 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "team-hub-team.dll"]
