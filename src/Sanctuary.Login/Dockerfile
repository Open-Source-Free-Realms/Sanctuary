FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 20042
EXPOSE 20041

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY src /src
RUN dotnet build "Sanctuary.sln" -c $BUILD_CONFIGURATION

FROM base AS final
ARG BUILD_CONFIGURATION=Release
WORKDIR /app
COPY --from=build /src/bin/$BUILD_CONFIGURATION .
ENTRYPOINT ["dotnet", "Sanctuary.Login.dll"]