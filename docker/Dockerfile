# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Oficina.sln", "./"]
COPY ["src/Oficina.Api/Oficina.Api.csproj",            "src/Oficina.Api/"]
COPY ["src/Oficina.Aplicacao/Oficina.Aplicacao.csproj", "src/Oficina.Aplicacao/"]
COPY ["src/Oficina.Dominio/Oficina.Dominio.csproj",     "src/Oficina.Dominio/"]
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
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_PRINT_TELEMETRY_MESSAGE=false
EXPOSE 8080
USER app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Oficina.Api.dll"]
