@echo off
echo =========================================
echo EMV Blacklist Management System
echo =========================================
echo.

REM Check if Docker is running
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo Error: Docker is not running
    echo Please start Docker Desktop and try again
    exit /b 1
)

echo Docker is running...
echo.

REM Ask which filter to run
echo Select filter to test:
echo 1) Cuckoo Filter
echo 2) Quotient Filter
echo 3) Hash Table + Golomb
echo 4) All filters (requires 8GB+ RAM)
echo.
set /p choice="Enter choice (1-4): "

if "%choice%"=="1" (
    echo.
    echo Starting Cuckoo Filter...
    docker-compose up --build api-cuckoo client-cuckoo
) else if "%choice%"=="2" (
    echo.
    echo Starting Quotient Filter...
    docker-compose up --build api-quotient client-quotient
) else if "%choice%"=="3" (
    echo.
    echo Starting Hash Table + Golomb Filter...
    docker-compose up --build api-golomb client-golomb
) else if "%choice%"=="4" (
    echo.
    echo Starting all filters...
    echo This requires at least 8GB RAM allocated to Docker
    docker-compose up --build
) else (
    echo Invalid choice
    exit /b 1
)
