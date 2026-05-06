@echo off
setlocal

chcp 65001 >nul 2>&1

set CONFIG=Release
set PROJECT=MinimalActorSystem.Benchmarks
set OUTPUT=benchmark_results.txt

echo ============================================
echo Actor System Benchmark
echo Configuration: %CONFIG%
echo Output: %OUTPUT%
echo ============================================
echo.

echo %date% %time% > %OUTPUT%
echo ============================================ >> %OUTPUT%
echo.

dotnet run -c %CONFIG% --project %PROJECT% >> %OUTPUT% 2>&1

echo.
echo ============================================
echo Done. Results saved to %OUTPUT%
echo ============================================
type %OUTPUT%
pause
