@echo off
echo =========================================
echo EMV Blacklist Filter Benchmarks
echo =========================================
echo.

REM Check if .NET 8 is installed
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo Error: .NET 8 SDK is not installed
    echo Please install from: https://dotnet.microsoft.com/download/dotnet/8.0
    exit /b 1
)

echo Building benchmark project...
cd src\EMVBlacklist.Benchmarks
dotnet build -c Release

if %errorlevel% neq 0 (
    echo Build failed!
    exit /b 1
)

echo.
echo Running benchmarks...
echo This may take several minutes...
echo.

dotnet run -c Release --no-build

echo.
echo =========================================
echo Benchmarks complete!
echo Results saved in BenchmarkDotNet.Artifacts\
echo =========================================

pause
