# Runs the C# tests with coverage and builds an HTML report.
# Run from the repo root:  ./coverage.ps1
$ErrorActionPreference = "Stop"

if (Test-Path TestResults) { Remove-Item TestResults -Recurse -Force }

dotnet test ClinicApi.Tests --collect:"XPlat Code Coverage" --results-directory ./TestResults

if (-not (Get-Command reportgenerator -ErrorAction SilentlyContinue)) {
    dotnet tool install -g dotnet-reportgenerator-globaltool
    $env:PATH += ";$env:USERPROFILE\.dotnet\tools"
}

reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"Html;TextSummary"

Get-Content coverage-report/Summary.txt | Select-Object -First 12
Start-Process coverage-report/index.html
