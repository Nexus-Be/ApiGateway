Write-Host "🚀 Starting Nexus API Gateway (.NET 8)..." -ForegroundColor Cyan
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project Nexus.Gateway --no-launch-profile --urls http://127.0.0.1:8080
