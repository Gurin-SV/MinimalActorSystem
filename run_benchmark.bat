@echo off
setlocal

chcp 65001 >nul 2>&1

set CONFIG=Release
set PROJECT=MinimalActorSystem.Benchmarks
set OUTPUT=benchmark_results.txt

echo Actor System Benchmark
echo Configuration: %CONFIG%
echo Output: %OUTPUT%
echo.

echo %date% %time% > %OUTPUT%
echo.

dotnet run -c %CONFIG% --project %PROJECT% >> %OUTPUT% 2>&1

echo.
type %OUTPUT%
pause
