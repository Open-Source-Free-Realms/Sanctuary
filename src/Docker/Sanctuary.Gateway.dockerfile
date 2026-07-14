FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 20260

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . /src
# Publish (not plain build) so the deps.json is correct: on .NET 10 a plain `dotnet build` marks the
# Microsoft.Extensions.* assemblies as framework-provided, so the console-app host won't probe /app
# for them and crashes at startup ("Could not load ...Configuration.Abstractions 10.0.0.0").
RUN dotnet publish "Sanctuary.Gateway/Sanctuary.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/out
# DB providers are loaded dynamically at runtime (Assembly.LoadFrom), so they are NOT project
# references of the gateway — publish them separately and merge their output (DLLs + native SQLite
# libs + deps.json) into the gateway dir without clobbering the gateway's own files.
RUN dotnet publish "Sanctuary.Database.SqLite/Sanctuary.Database.Sqlite.csproj" -c $BUILD_CONFIGURATION -o /app/sqlite
RUN dotnet publish "Sanctuary.Database.MySql/Sanctuary.Database.MySql.csproj" -c $BUILD_CONFIGURATION -o /app/mysql
RUN cp -rn /app/sqlite/. /app/out/ && cp -rn /app/mysql/. /app/out/

FROM base AS final
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "Sanctuary.Gateway.dll"]
