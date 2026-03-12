FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Workcast.Api/Workcast.Api.csproj \
    -c Release -o /app/publish

# mcr.microsoft.com/playwright/dotnet includes the ASP.NET Core runtime,
# all Playwright system dependencies, and pre-installed browser binaries.
# This replaces the manual apt-get + playwright install steps from the spec,
# which relied on the now-removed Microsoft.Playwright.CLI dotnet tool.
FROM mcr.microsoft.com/playwright/dotnet:v1.44.0-jammy AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Workcast.Api.dll"]
