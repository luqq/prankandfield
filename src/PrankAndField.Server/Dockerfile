# Relay server image. Build from the repository root:
#   docker build -f src/PrankAndField.Server/Dockerfile -t prankandfield-server .
# Multi-arch (amd64 + arm64 for an Ampere VM):
#   docker buildx build --platform linux/amd64,linux/arm64 -f src/PrankAndField.Server/Dockerfile .

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /repo
COPY global.json Directory.Build.props ./
COPY src/PrankAndField.Server/PrankAndField.Server.csproj src/PrankAndField.Server/
RUN dotnet restore src/PrankAndField.Server/PrankAndField.Server.csproj -a $TARGETARCH
COPY src/PrankAndField.Server/ src/PrankAndField.Server/
RUN dotnet publish src/PrankAndField.Server/PrankAndField.Server.csproj -a $TARGETARCH --no-restore -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "PrankAndField.Server.dll"]
