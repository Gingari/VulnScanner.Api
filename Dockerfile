FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

COPY VulnScanner.csproj ./
RUN dotnet restore VulnScanner.csproj

COPY . .
RUN dotnet publish VulnScanner.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM ubuntu:24.04 AS tools

ARG GITLEAKS_VERSION
ARG TRIVY_VERSION

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /downloads

RUN test -n "$GITLEAKS_VERSION" \
    && test -n "$TRIVY_VERSION" \
    && mkdir -p /tools \
    && curl -fsSL \
       "https://github.com/gitleaks/gitleaks/releases/download/v${GITLEAKS_VERSION}/gitleaks_${GITLEAKS_VERSION}_linux_x64.tar.gz" \
       -o gitleaks.tar.gz \
    && tar -xzf gitleaks.tar.gz -C /tools gitleaks \
    && curl -fsSL \
       "https://github.com/aquasecurity/trivy/releases/download/v${TRIVY_VERSION}/trivy_${TRIVY_VERSION}_Linux-64bit.tar.gz" \
       -o trivy.tar.gz \
    && tar -xzf trivy.tar.gz -C /tools trivy \
    && chmod 755 /tools/gitleaks /tools/trivy

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime

USER root

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       git ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /work/tmp /work/cache \
    && chown -R app:app /work

WORKDIR /app

COPY --from=build /app/publish/ ./
COPY --from=tools /tools/gitleaks /usr/local/bin/gitleaks
COPY --from=tools /tools/trivy /usr/local/bin/trivy

ENV ASPNETCORE_HTTP_PORTS=8080 \
    TMPDIR=/work/tmp \
    TRIVY_CACHE_DIR=/work/cache

USER app

EXPOSE 8080

ENTRYPOINT ["dotnet", "VulnScanner.dll"]