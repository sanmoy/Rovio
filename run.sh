#!/bin/bash

# Start all services for local development/testing
echo "Starting Matchmaking.Storage on http://localhost:5010..."
dotnet run --project src/Matchmaking.Storage --launch-profile http &
sleep 3

echo "Starting Matchmaking.Api on http://localhost:5000..."
dotnet run --project src/Matchmaking.Api --launch-profile http &
sleep 2

echo "Starting Matchmaking.Worker..."
dotnet run --project src/Matchmaking.Worker &
sleep 2

echo "Starting Matchmaking.MockGameServer on http://localhost:5020..."
dotnet run --project src/Matchmaking.MockGameServer --launch-profile http &

echo "All services started! Press Ctrl+C to stop the script. Background processes might need to be terminated."
wait
