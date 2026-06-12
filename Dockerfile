# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /build

COPY src/Api/Api.csproj ./Api/
RUN dotnet restore ./Api/Api.csproj -r linux-musl-x64

COPY src/Api/ ./Api/
WORKDIR /build/Api
RUN dotnet publish -c Release -o /app/publish \
    --no-restore \
    -r linux-musl-x64 \
    --self-contained false

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

RUN apk add --no-cache icu-libs krb5-libs libgcc libintl libssl3 libstdc++ zlib
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

COPY --from=build --chown=appuser:appgroup /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]
