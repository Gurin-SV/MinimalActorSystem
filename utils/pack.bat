@echo off
echo Building ActorSystem...
dotnet build ActorSystem.sln -c Release
if %ERRORLEVEL% NEQ 0 (
    echo Build failed with error %ERRORLEVEL%.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Clearing NuGet cache...
dotnet nuget locals global-packages --clear

echo.
echo Creating NuGet packages...
if not exist build\packages mkdir build\packages
dotnet pack MinimalActorSystem\MinimalActorSystem.csproj -c Release -o build\packages
if %ERRORLEVEL% NEQ 0 (
    echo Pack failed with error %ERRORLEVEL%.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Done. Packages:
dir build\packages\*.nupkg
pause
