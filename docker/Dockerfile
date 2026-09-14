# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Oficina.sln", "./"]
COPY ["src/Oficina.Api/Oficina.Api.csproj",            "src/Oficina.Api/"]
COPY ["src/Oficina.Aplicacao/Oficina.Aplicacao.csproj", "src/Oficina.Aplicacao/"]
COPY ["src/Oficina.Dominio/Oficina.Dominio.csproj",     "src/Oficina.Dominio/"]
COPY ["src/Oficina.Adaptadores/Oficina.Adaptadores.csproj", "src/Oficina.Adaptadores/"]
COPY ["src/Oficina.Infraestrutura/Oficina.Infraestrutura.csproj", "src/Oficina.Infraestrutura/"]
RUN dotnet restore src/Oficina.Api/Oficina.Api.csproj

COPY src/ src/
RUN dotnet publish src/Oficina.Api/Oficina.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl instalado minimamente apenas para o HEALTHCHECK do container (o /health e anonimo).
# Em producao o mecanismo de saude e o probe do Kubernetes; aqui e conveniencia para compose/docker ps.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_PRINT_TELEMETRY_MESSAGE=false
EXPOSE 8080

LABEL org.opencontainers.image.title="Oficina Mecanica API" \
      org.opencontainers.image.description="MVP back-end da oficina (FIAP 15SOAT Fase 2)" \
      org.opencontainers.image.source="https://github.com/lcr-thiago-fernandes/fiap_15SOAT_fase1" \
      org.opencontainers.image.licenses="MIT"

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

USER app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Oficina.Api.dll"]
