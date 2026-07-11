# ---------- shared ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-deps
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY global.json Directory.Packages.props Sanctuary.sln ./

COPY Sanctuary.Core/Sanctuary.Core.csproj Sanctuary.Core/
COPY Sanctuary.Packet.Common/Sanctuary.Packet.Common.csproj Sanctuary.Packet.Common/
COPY Sanctuary.Packet/Sanctuary.Packet.csproj Sanctuary.Packet/
COPY Sanctuary.UdpLibrary/Sanctuary.UdpLibrary.csproj Sanctuary.UdpLibrary/
COPY Sanctuary.UdpLibrary.Tests/Sanctuary.UdpLibrary.Tests.csproj Sanctuary.UdpLibrary.Tests/
COPY Sanctuary.Database.Entities/Sanctuary.Database.Entities.csproj Sanctuary.Database.Entities/
COPY Sanctuary.Database/Sanctuary.Database.csproj Sanctuary.Database/
COPY Sanctuary.Database.MySql/Sanctuary.Database.MySql.csproj Sanctuary.Database.MySql/
COPY Sanctuary.Database.SqLite/Sanctuary.Database.Sqlite.csproj Sanctuary.Database.SqLite/
COPY Sanctuary.Game/Sanctuary.Game.csproj Sanctuary.Game/
COPY Sanctuary.Gateway/Sanctuary.Gateway.csproj Sanctuary.Gateway/
COPY Sanctuary.Login/Sanctuary.Login.csproj Sanctuary.Login/
COPY Sanctuary.WebAPI/Sanctuary.WebAPI.csproj Sanctuary.WebAPI/

RUN dotnet restore Sanctuary.sln

COPY Sanctuary.Core/ Sanctuary.Core/
COPY Sanctuary.Packet.Common/ Sanctuary.Packet.Common/
COPY Sanctuary.Packet/ Sanctuary.Packet/
COPY Sanctuary.UdpLibrary/ Sanctuary.UdpLibrary/
COPY Sanctuary.Database.Entities/ Sanctuary.Database.Entities/
COPY Sanctuary.Database/ Sanctuary.Database/
COPY Sanctuary.Database.MySql/ Sanctuary.Database.MySql/
COPY Sanctuary.Database.SqLite/ Sanctuary.Database.SqLite/
COPY Sanctuary.Game/ Sanctuary.Game/

RUN dotnet build Sanctuary.Game/Sanctuary.Game.csproj                       -c $BUILD_CONFIGURATION --no-restore \
RUN dotnet build Sanctuary.Database.MySql/Sanctuary.Database.MySql.csproj   -c $BUILD_CONFIGURATION --no-restore \
RUN dotnet build Sanctuary.Database.SqLite/Sanctuary.Database.Sqlite.csproj -c $BUILD_CONFIGURATION --no-restore

# ---------- build ----------
FROM build-deps AS build-gateway
ARG BUILD_CONFIGURATION=Release
COPY Sanctuary.Gateway/ Sanctuary.Gateway/
RUN dotnet build Sanctuary.Gateway/Sanctuary.Gateway.csproj -c $BUILD_CONFIGURATION --no-restore --no-dependencies

FROM build-deps AS build-login
ARG BUILD_CONFIGURATION=Release
COPY Sanctuary.Login/ Sanctuary.Login/
RUN dotnet build Sanctuary.Login/Sanctuary.Login.csproj -c $BUILD_CONFIGURATION --no-restore --no-dependencies

FROM build-deps AS build-webapi
ARG BUILD_CONFIGURATION=Release
COPY Sanctuary.WebAPI/ Sanctuary.WebAPI/
RUN dotnet build Sanctuary.WebAPI/Sanctuary.WebAPI.csproj -c $BUILD_CONFIGURATION --no-restore --no-dependencies

# ---------- runtime base ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
ARG BUILD_CONFIGURATION=Release
WORKDIR /app

# ---------- runtime ----------
FROM base AS gateway
EXPOSE 20260
COPY --from=build-gateway /src/**/bin/$BUILD_CONFIGURATION .
ENTRYPOINT ["dotnet", "Sanctuary.Gateway.dll"]

FROM base AS login
EXPOSE 20042
EXPOSE 20041
COPY --from=build-login /src/**/bin/$BUILD_CONFIGURATION .
ENTRYPOINT ["dotnet", "Sanctuary.Login.dll"]

FROM base AS webapi
EXPOSE 20040
COPY --from=build-webapi /src/**/bin/$BUILD_CONFIGURATION .
ENTRYPOINT ["dotnet", "Sanctuary.WebAPI.dll"]
