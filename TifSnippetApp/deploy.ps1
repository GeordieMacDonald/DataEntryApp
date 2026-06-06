# TifSnippetApp Deployment Script
# This script automates the build and run process for the demo.

$ErrorActionPreference = "Stop"

Write-Host "--- TifSnippetApp Deployment Script ---" -ForegroundColor Cyan

# 1. Check for .NET 9.0
Write-Host "[1/4] Checking Prerequisites..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version 2>$null
if ($null -eq $dotnetVersion -or $dotnetVersion -notmatch "^9\.") {
    Write-Error "Error: .NET 9.0 SDK is required but was not found. Please install it from https://dotnet.microsoft.com/download/dotnet/9.0"
    exit 1
}
Write-Host "Found .NET Version: $dotnetVersion" -ForegroundColor Green

# 2. Build the Solution
Write-Host "[2/4] Building Solution..." -ForegroundColor Yellow
dotnet build
if ($LASTEXITCODE -ne 0) {
    Write-Error "Error: Build failed."
    exit $LASTEXITCODE
}
Write-Host "Build Successful!" -ForegroundColor Green

# 3. Identify and Run the Server
Write-Host "[3/4] Launching Application..." -ForegroundColor Yellow
$serverDir = Join-Path $PSScriptRoot "TifSnippetApp"
if (-not (Test-Path $serverDir)) {
    Write-Error "Error: Could not find server project at $serverDir"
    exit 1
}

# Run in background and wait for it to start
Write-Host "Starting server on http://localhost:5087..." -ForegroundColor Cyan
$process = Start-Process dotnet -ArgumentList "run", "--project", "$serverDir" -NoNewWindow -PassThru

# 4. Open Browser
Write-Host "[4/4] Opening Web Browser..." -ForegroundColor Yellow
Start-Sleep -Seconds 3 # Give it a moment to start
Start-Process "http://localhost:5087"

Write-Host "--- Deployment Complete ---" -ForegroundColor Green
Write-Host "Press Ctrl+C in this terminal to stop the server." -ForegroundColor Gray

# Wait for process to exit (user stops it)
$process | Wait-Process
