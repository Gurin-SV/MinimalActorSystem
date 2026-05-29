@echo off
setlocal

chcp 65001 >nul 2>&1

set CONFIG=Release
set PROJECT=tests\MinimalActorSystem.Benchmarks\MinimalActorSystem.Benchmarks.csproj
set OUTPUT=benchmark_results.txt
set LANG=en

echo Actor System Benchmark
echo Configuration: %CONFIG%
echo Language: %LANG%
echo Project: %PROJECT%
echo Output: %OUTPUT%
echo.
echo WARNING: The test takes about one minute. Please wait...
echo.
echo %date% %time% > %OUTPUT%
echo.

dotnet run -c %CONFIG% --project %PROJECT% -- %LANG% >> %OUTPUT% 2>&1

echo.
echo === RESULTS ===
echo.
type %OUTPUT%
pause