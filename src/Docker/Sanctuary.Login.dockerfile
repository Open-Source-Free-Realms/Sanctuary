FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 20042
EXPOSE 20041

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . /src
# Publish (not plain build) so the deps.json is correct on .NET 10 (see gateway Dockerfile).
RUN dotnet publish "Sanctuary.Login/Sanctuary.Login.csproj" -c $BUILD_CONFIGURATION -o /app/out
# DB providers are loaded dynamically (Assembly.LoadFrom) — publish + merge their output.
RUN dotnet publish "Sanctuary.Database.SqLite/Sanctuary.Database.Sqlite.csproj" -c $BUILD_CONFIGURATION -o /app/sqlite
RUN dotnet publish "Sanctuary.Database.MySql/Sanctuary.Database.MySql.csproj" -c $BUILD_CONFIGURATION -o /app/mysql
RUN cp -rn /app/sqlite/. /app/out/ && cp -rn /app/mysql/. /app/out/

FROM base AS final
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "Sanctuary.Login.dll"]
