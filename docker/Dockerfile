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

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
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
      org.opencontainers.image.description="Back-end da oficina (FIAP 15SOAT Fase 3)" \
      org.opencontainers.image.source="https://github.com/lcr-thiago-fernandes/oficina-app" \
      org.opencontainers.image.licenses="MIT"

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

# Agente APM do New Relic, instalado via tarball oficial (nao via apt).
# O repositorio apt.newrelic.com exige duas chaves GPG (a assinatura do Release
# depende de B31B29E5548C16BF e de 32FA4C172DAD550E) que na pratica ficam fora
# de sincronia com o InRelease publicado, quebrando o "apt-get update" com
# NO_PUBKEY. O tarball oficial para Linux sem gerenciador de pacotes evita essa
# fragilidade e usa o mesmo curl ja instalado acima.
# Versao fixada deliberadamente (nao "latest_release"): um upgrade do agente
# e um bump proprio, com commit e diff revisaveis, do mesmo jeito que um
# pacote NuGet. O hash sha256 abaixo veio do arquivo .sha256 publicado pela
# New Relic para esta mesma versao e protege contra origem comprometida ou
# corrupcao silenciosa do download — o arquivo baixado e uma biblioteca
# nativa (libNewRelicProfiler.so) carregada dentro do processo do CLR.
# Para atualizar: troque a versao no path, no nome do arquivo e no hash.
ARG NEWRELIC_DOTNET_AGENT_VERSION=10.54.0
ARG NEWRELIC_DOTNET_AGENT_SHA256=f0db7c0ee61d1541b53e4f2c386ffc2a1e9eaf8a2747851addb609413c7c9716
RUN curl -fsSL -o /tmp/newrelic-dotnet-agent.tar.gz \
        "https://download.newrelic.com/dot_net_agent/previous_releases/${NEWRELIC_DOTNET_AGENT_VERSION}/newrelic-dotnet-agent_${NEWRELIC_DOTNET_AGENT_VERSION}_amd64.tar.gz" \
    && echo "${NEWRELIC_DOTNET_AGENT_SHA256}  /tmp/newrelic-dotnet-agent.tar.gz" | sha256sum -c - \
    && mkdir -p /usr/local/newrelic-dotnet-agent \
    && tar -xzf /tmp/newrelic-dotnet-agent.tar.gz -C /usr/local/newrelic-dotnet-agent --strip-components=1 \
    && rm -f /tmp/newrelic-dotnet-agent.tar.gz

# CORECLR_ENABLE_PROFILING=0 por padrao: quem carrega o profiler no CLR e essa
# variavel, nao a licenca. Com =1 e sem NEW_RELIC_LICENSE_KEY o profiler E
# carregado, so nao consegue reportar telemetria — por isso o Deployment do
# Kubernetes (tarefa futura) e quem liga o profiling, nunca esta imagem local.
ENV CORECLR_ENABLE_PROFILING=0 \
    CORECLR_PROFILER="{36032161-FFC0-4B61-B559-F6C5D41BAE5A}" \
    CORECLR_NEWRELIC_HOME=/usr/local/newrelic-dotnet-agent \
    CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so \
    NEW_RELIC_APP_NAME="oficina-api"

USER app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Oficina.Api.dll"]
