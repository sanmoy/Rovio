# Start all services for local development/testing
Write-Host "Starting Matchmaking.Storage on http://localhost:5010..." -ForegroundColor Green
Start-Process dotnet -ArgumentList "run --project src/Matchmaking.Storage --launch-profile http"
Start-Sleep -Seconds 3

Write-Host "Starting Matchmaking.Api on http://localhost:5000..." -ForegroundColor Green
Start-Process dotnet -ArgumentList "run --project src/Matchmaking.Api --launch-profile http"
Start-Sleep -Seconds 2

Write-Host "Starting Matchmaking.Worker..." -ForegroundColor Green
Start-Process dotnet -ArgumentList "run --project src/Matchmaking.Worker"
Start-Sleep -Seconds 2

Write-Host "Starting Matchmaking.MockGameServer on http://localhost:5020..." -ForegroundColor Green
Start-Process dotnet -ArgumentList "run --project src/Matchmaking.MockGameServer --launch-profile http"

Write-Host "All services started!" -ForegroundColor Green
