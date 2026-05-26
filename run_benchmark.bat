@echo off
setlocal

chcp 65001 >nul 2>&1

set CONFIG=Release
set PROJECT=tests\MinimalActorSystem.Benchmarks\MinimalActorSystem.Benchmarks.csproj
set OUTPUT=benchmark_results.txt

echo Actor System Benchmark
echo Configuration: %CONFIG%
echo Project: %PROJECT%
echo Output: %OUTPUT%
echo.

echo %date% %time% > %OUTPUT%
echo.

dotnet run -c %CONFIG% --project %PROJECT% >> %OUTPUT% 2>&1

echo.
type %OUTPUT%
pause
