FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Workcast.Api/Workcast.Api.csproj \
    -c Release -o /app/publish

# Used only as a browser-binary source. We do not inherit its .NET runtime
# (which is .NET 8). The aspnet:10.0-jammy stage below provides the runtime.
# Both stages are Ubuntu 22.04 (jammy) so the Chromium binary's shared-library
# dependencies are satisfied without version-skew.
FROM mcr.microsoft.com/playwright:v1.62.0-jammy AS playwright-browsers

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Chromium system dependencies — mirrors what the playwright/dotnet image ships.
RUN apt-get update && apt-get install -y --no-install-recommends \
    libglib2.0-0 \
    libnss3 \
    libnspr4 \
    libdbus-1-3 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libxkbcommon0 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxrandr2 \
    libgbm1 \
    libasound2t64 \
    libpango-1.0-0 \
    libcairo2 \
    && rm -rf /var/lib/apt/lists/*

# Copy pre-installed browser binaries from the playwright stage.
COPY --from=playwright-browsers /ms-playwright /ms-playwright

# Tell Playwright where the browsers live.
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Workcast.Api.dll"]
