#!/usr/bin/env pwsh
# Script to build and run all PDF generation projects

Write-Host "Building all projects..." -ForegroundColor Cyan
dotnet build nreco.sln

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`nRunning NReco project..." -ForegroundColor Green
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
dotnet run --project NReco/NReco.csproj
$elapsed = $stopwatch.Elapsed
Write-Host ("Elapsed time: {0} ({1} seconds)" -f $elapsed.ToString("hh\:mm\:ss\.fff"), [math]::Round($elapsed.TotalSeconds, 3)) -ForegroundColor Yellow

Write-Host "`nRunning PuppeteerSharp project..." -ForegroundColor Green
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
dotnet run --project PuppeteerSharp/PuppeteerSharp.csproj
$elapsed = $stopwatch.Elapsed
Write-Host ("Elapsed time: {0} ({1} seconds)" -f $elapsed.ToString("hh\:mm\:ss\.fff"), [math]::Round($elapsed.TotalSeconds, 3)) -ForegroundColor Yellow

Write-Host "`nRunning DinkToPdf project..." -ForegroundColor Green
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
dotnet run --project DinkToPdf/DinkToPdf.csproj
$elapsed = $stopwatch.Elapsed
Write-Host ("Elapsed time: {0} ({1} seconds)" -f $elapsed.ToString("hh\:mm\:ss\.fff"), [math]::Round($elapsed.TotalSeconds, 3)) -ForegroundColor Yellow

Write-Host "`nRunning SelectPdf project..." -ForegroundColor Green
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
dotnet run --project SelectPdf/SelectPdf.csproj
$elapsed = $stopwatch.Elapsed
Write-Host ("Elapsed time: {0} ({1} seconds)" -f $elapsed.ToString("hh\:mm\:ss\.fff"), [math]::Round($elapsed.TotalSeconds, 3)) -ForegroundColor Yellow

Write-Host "`nRunning IronPdfExample project..." -ForegroundColor Green
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
dotnet run --project IronPdfExample/IronPdfExample.csproj
$elapsed = $stopwatch.Elapsed
Write-Host ("Elapsed time: {0} ({1} seconds)" -f $elapsed.ToString("hh\:mm\:ss\.fff"), [math]::Round($elapsed.TotalSeconds, 3)) -ForegroundColor Yellow

Write-Host "`nRunning Syncfusion project..." -ForegroundColor Green
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
dotnet run --project Syncfusion/Syncfusion.csproj
$elapsed = $stopwatch.Elapsed
Write-Host ("Elapsed time: {0} ({1} seconds)" -f $elapsed.ToString("hh\:mm\:ss\.fff"), [math]::Round($elapsed.TotalSeconds, 3)) -ForegroundColor Yellow

Write-Host "`nRunning SpirePdf project..." -ForegroundColor Green
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
dotnet run --project SpirePdf/SpirePdf.csproj
$elapsed = $stopwatch.Elapsed
Write-Host ("Elapsed time: {0} ({1} seconds)" -f $elapsed.ToString("hh\:mm\:ss\.fff"), [math]::Round($elapsed.TotalSeconds, 3)) -ForegroundColor Yellow

Write-Host "`nAll projects completed!" -ForegroundColor Cyan
