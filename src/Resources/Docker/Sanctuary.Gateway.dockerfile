FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 20260

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . /src
RUN dotnet build "Sanctuary.Gateway/Sanctuary.Gateway.csproj" -c $BUILD_CONFIGURATION
RUN dotnet build "Sanctuary.Database.MySql/Sanctuary.Database.MySql.csproj" -c $BUILD_CONFIGURATION
RUN dotnet build "Sanctuary.Database.SqLite/Sanctuary.Database.Sqlite.csproj" -c $BUILD_CONFIGURATION
RUN dotnet build "Sanctuary.Database.SqlServer/Sanctuary.Database.SqlServer.csproj" -c $BUILD_CONFIGURATION

FROM base AS final
ARG BUILD_CONFIGURATION=Release
WORKDIR /app
COPY --from=build /src/**/bin/$BUILD_CONFIGURATION .
ENTRYPOINT ["dotnet", "Sanctuary.Gateway.dll"]