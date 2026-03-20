@echo off
echo Starting Workcast (backend + frontend)...

powershell src/Workcast.Api/bin/Debug/net10.0/playwright.ps1 install chromium
start "Workcast API" cmd /k "cd /d %~dp0src\Workcast.Api && dotnet run --launch-profile http"
start "Workcast Web" cmd /k "cd /d %~dp0web && npm run dev"

echo.
echo API  : http://localhost:8080
echo Web  : http://localhost:3000
echo Swagger: http://localhost:8080/swagger
