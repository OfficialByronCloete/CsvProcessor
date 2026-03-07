@echo off
cd /d "%~dp0"
cls
echo Starting services...
echo This may take a few minutes on first run.
echo.

where docker >nul 2>&1
if errorlevel 1 (
  echo Docker is not installed or not available in PATH.
  pause
  exit /b 1
)

echo Building backend image...
docker compose build backend > compose-startup.log 2>&1
if errorlevel 1 (
  goto :startup_failed
)
echo Backend image created.

echo Building frontend image...
docker compose build frontend >> compose-startup.log 2>&1
if errorlevel 1 (
  goto :startup_failed
)
echo Frontend image created.

echo Starting containers...
docker compose up -d --no-build >> compose-startup.log 2>&1
if errorlevel 1 (
  goto :startup_failed
)
echo Services started.
echo.
echo Frontend: http://localhost:9000
echo Backend : http://localhost:5062/swagger
echo.
echo Use "docker compose logs -f" to follow logs.
pause
exit /b 0

:startup_failed
echo.
echo Failed to start services. See compose-startup.log for details.
pause
exit /b 1
