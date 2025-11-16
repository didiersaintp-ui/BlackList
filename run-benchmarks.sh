#!/bin/bash

echo "========================================="
echo "EMV Blacklist Filter Benchmarks"
echo "========================================="
echo ""

# Check if .NET 8 is installed
if ! command -v dotnet &> /dev/null
then
    echo "Error: .NET 8 SDK is not installed"
    echo "Please install from: https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
fi

echo "Building benchmark project..."
cd src/EMVBlacklist.Benchmarks
dotnet build -c Release

if [ $? -ne 0 ]; then
    echo "Build failed!"
    exit 1
fi

echo ""
echo "Running benchmarks..."
echo "This may take several minutes..."
echo ""

dotnet run -c Release --no-build

echo ""
echo "========================================="
echo "Benchmarks complete!"
echo "Results saved in BenchmarkDotNet.Artifacts/"
echo "========================================="
