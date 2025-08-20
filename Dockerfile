# Use the official .NET SDK image to build the application (multi-stage optimized)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Add build arguments
ARG APP_VERSION=1.3.0
ARG BUILD_DATE
ARG BUILD_CONFIGURATION=Release

WORKDIR /app

# Copy only the project file first for better layer caching
COPY *.csproj ./

# Restore dependencies with optimizations
RUN dotnet restore --runtime linux-arm64 --no-cache

# Copy source code (exclude unnecessary files with .dockerignore)
COPY . ./

# Build with optimizations
RUN dotnet publish -c ${BUILD_CONFIGURATION} -o out \
    --runtime linux-arm64 \
    --self-contained true \
    --no-restore \
    --framework net8.0

# Verify build output
RUN ls -la /app/out/ && echo "Build completed successfully"

# Production runtime stage (optimized) - Using Ubuntu base for self-contained
FROM ubuntu:22.04 AS runtime

# Add build arguments
ARG APP_VERSION=1.3.0
ARG BUILD_DATE

# Set metadata labels
LABEL maintainer="Election API Team" \
      version="${APP_VERSION}" \
      build-date="${BUILD_DATE}" \
      description="Election API .NET - Secure Voting System"

WORKDIR /app

# Create non-root user for security
RUN groupadd -r electionapi && useradd -r -g electionapi electionapi

# Install dependencies with minimal footprint
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    ca-certificates \
    tzdata \
    libicu70 \
    && rm -rf /var/lib/apt/lists/* \
    && apt-get clean \
    && ln -sf /usr/share/zoneinfo/America/Sao_Paulo /etc/localtime \
    && echo "America/Sao_Paulo" > /etc/timezone

# Copy published application with proper ownership
COPY --from=build --chown=electionapi:electionapi /app/out .

# Create directories with proper permissions
RUN mkdir -p /app/wwwroot/uploads/candidates /app/logs /tmp/app \
    && chown -R electionapi:electionapi /app/wwwroot /app/logs /tmp/app \
    && chmod 755 /app/wwwroot/uploads/candidates

# Create optimized startup script with health checks
RUN echo '#!/bin/bash\n\
set -euo pipefail\n\
echo "==============================================="\n\
echo "🚀 Election API .NET v${APP_VERSION:-1.3.0}"\n\
echo "📅 Build: ${BUILD_DATE:-Unknown}"\n\
echo "🕐 Timezone: America/Sao_Paulo (BRT)"\n\
echo "==============================================="\n\
echo "⚡ Production Features:"\n\
echo "  ⚠️  Two-Factor Authentication (TEMPORARILY DISABLED)"\n\
echo "  🔒 System Seal Verification"\n\
echo "  👑 Master User System"\n\
echo "  📊 Advanced Audit Logging"\n\
echo "  📸 Hybrid Photo System"\n\
echo "  🏢 Company Management System"\n\
echo "  ⚙️  Database Triggers (Auto Company & Seal)"\n\
echo "  🛡️  Security Hardened"\n\
echo "==============================================="\n\
echo "⏳ Waiting for dependencies..."\n\
timeout=60\n\
counter=0\n\
while [ $counter -lt $timeout ]; do\n\
  if [ -n "${DB_HOST:-}" ] && nc -z "${DB_HOST}" "${DB_PORT:-3306}" 2>/dev/null; then\n\
    echo "✅ Database connection available"\n\
    break\n\
  fi\n\
  echo "⏳ Waiting for database... ($counter/$timeout)"\n\
  sleep 2\n\
  counter=$((counter + 2))\n\
done\n\
echo "🔍 Debug: Checking files in /app..."\n\
ls -la /app/\n\
echo "🔍 Debug: Checking runtime config..."\n\
cat /app/ElectionApi.Net.runtimeconfig.json\n\
echo "🔍 Debug: Checking executable permissions..."\n\
ls -la /app/ElectionApi.Net\n\
echo "🌟 Starting Election API..."\n\
exec ./ElectionApi.Net' > /app/start.sh \
    && chmod +x /app/start.sh \
    && chown electionapi:electionapi /app/start.sh

# Install netcat for connectivity checks
RUN apt-get update && apt-get install -y --no-install-recommends netcat-openbsd \
    && rm -rf /var/lib/apt/lists/* && apt-get clean

# Expose the port the application will run on
EXPOSE 5110

# Set environment variables for production
ENV ASPNETCORE_URLS=http://0.0.0.0:5110 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    APP_VERSION=${APP_VERSION} \
    TZ=America/Sao_Paulo \
    ASPNETCORE_ENVIRONMENT=Production

# Switch to non-root user for security
USER electionapi

# Add health check
HEALTHCHECK --interval=30s --timeout=30s --start-period=40s --retries=5 \
    CMD curl -f http://localhost:5110/health

# Set the entry point to use our startup script
ENTRYPOINT ["/app/start.sh"]
