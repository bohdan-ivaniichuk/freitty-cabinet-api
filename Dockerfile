FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first: a code change does not invalidate the restored packages layer.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Freitty.Domain/Freitty.Domain.csproj src/Freitty.Domain/
COPY src/Freitty.Application/Freitty.Application.csproj src/Freitty.Application/
COPY src/Freitty.Infrastructure/Freitty.Infrastructure.csproj src/Freitty.Infrastructure/
COPY src/Freitty.Api/Freitty.Api.csproj src/Freitty.Api/
RUN dotnet restore src/Freitty.Api/Freitty.Api.csproj

COPY src/ src/
RUN dotnet publish src/Freitty.Api/Freitty.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl serves the compose health check; the base image ships no HTTP client.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Freitty.Api.dll"]
