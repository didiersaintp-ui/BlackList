#!/bin/bash

echo "========================================="
echo "EMV Blacklist Management System"
echo "========================================="
echo ""

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "Error: Docker is not running"
    echo "Please start Docker Desktop and try again"
    exit 1
fi

echo "Docker is running..."
echo ""

# Ask which filter to run
echo "Select filter to test:"
echo "1) Cuckoo Filter"
echo "2) Quotient Filter"
echo "3) Hash Table + Golomb"
echo "4) All filters (requires 8GB+ RAM)"
echo ""
read -p "Enter choice (1-4): " choice

case $choice in
    1)
        echo ""
        echo "Starting Cuckoo Filter..."
        docker-compose up --build api-cuckoo client-cuckoo
        ;;
    2)
        echo ""
        echo "Starting Quotient Filter..."
        docker-compose up --build api-quotient client-quotient
        ;;
    3)
        echo ""
        echo "Starting Hash Table + Golomb Filter..."
        docker-compose up --build api-golomb client-golomb
        ;;
    4)
        echo ""
        echo "Starting all filters..."
        echo "This requires at least 8GB RAM allocated to Docker"
        docker-compose up --build
        ;;
    *)
        echo "Invalid choice"
        exit 1
        ;;
esac
