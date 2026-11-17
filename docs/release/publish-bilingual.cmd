@echo off
chcp 65001 >nul
dotnet run --project "%~dp0..\..\tools\WorkshopPublisher\WorkshopPublisher.csproj" -c Release -- --publish "%~dp0workshop-0.2.0.vdf"
set "publishResult=%errorlevel%"
if not "%publishResult%"=="0" (
  echo Publication or verification failed. Check the messages above before repeating.
) else (
  echo English and Simplified Chinese descriptions and tags updated and verified.
)
pause
exit /b %publishResult%
