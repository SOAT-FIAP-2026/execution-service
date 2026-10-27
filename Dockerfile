# syntax=docker/dockerfile:1

# ---------- Stage 1: Build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore em camada separada (cache) copiando apenas os .csproj
COPY src/Fiap.TechChallenge.Execution.Domain/Fiap.TechChallenge.Execution.Domain.csproj src/Fiap.TechChallenge.Execution.Domain/
COPY src/Fiap.TechChallenge.Execution.Application/Fiap.TechChallenge.Execution.Application.csproj src/Fiap.TechChallenge.Execution.Application/
COPY src/Fiap.TechChallenge.Execution.Infrastructure/Fiap.TechChallenge.Execution.Infrastructure.csproj src/Fiap.TechChallenge.Execution.Infrastructure/
COPY src/Fiap.TechChallenge.Execution.Api/Fiap.TechChallenge.Execution.Api.csproj src/Fiap.TechChallenge.Execution.Api/

RUN dotnet restore src/Fiap.TechChallenge.Execution.Api/Fiap.TechChallenge.Execution.Api.csproj

COPY src/ src/
RUN dotnet publish src/Fiap.TechChallenge.Execution.Api/Fiap.TechChallenge.Execution.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- Stage 2: Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

# Usuário non-root já existente na imagem oficial (.NET 8): "app" (UID 1654)
COPY --from=build --chown=app:app /app/publish .
USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "Fiap.TechChallenge.Execution.Api.dll"]
