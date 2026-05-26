@echo off
echo Building MinimalActorSystem...

dotnet build src\MinimalActorSystem\MinimalActorSystem.csproj -c Release
if %ERRORLEVEL% NEQ 0 (
    echo Build failed.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Creating NuGet package...

if not exist build\packages mkdir build\packages
dotnet pack src\MinimalActorSystem\MinimalActorSystem.csproj -c Release -o build\packages --no-build

echo.
echo Done.
dir build\packages\*.nupkg
pause