#!/usr/bin/env pwsh
# Sequential runner: runs each project once, but each project generates N PDFs sequentially in a single process.
# Usage examples:
#   pwsh ./run-sequential.ps1               # prompts for PDF count, builds Release, runs all projects
#   pwsh ./run-sequential.ps1 -PdfCount 10 # generates 10 sequential PDFs per project
#   pwsh ./run-sequential.ps1 -PdfCount 5 -Configuration Debug
#   pwsh ./run-sequential.ps1 -PdfCount 8 -Projects @('NReco/NReco.csproj','PuppeteerSharp/PuppeteerSharp.csproj')

[CmdletBinding()]
param(
  [int]$PdfCount,
  [ValidateSet('Debug','Release')]
  [string]$Configuration = 'Release',
  [string[]]$Projects,
  [int]$PerProcessTimeoutSec = 300
)

$ErrorActionPreference = 'Stop'

function Read-PdfCount([string]$Prompt = 'Enter number of PDFs to generate sequentially per project') {
  while ($true) {
    $val = Read-Host $Prompt
    $parsed = 0
    if ([int]::TryParse($val, [ref]$parsed) -and $parsed -gt 0) {
      return $parsed
    }
    Write-Host "Please enter a valid positive integer." -ForegroundColor Yellow
  }
}

# Default project list in the desired order
if (-not $Projects -or $Projects.Count -eq 0) {
  $Projects = @(
    'NReco/NReco.csproj',
    'PuppeteerSharp/PuppeteerSharp.csproj',
    'DinkToPdf/DinkToPdf.csproj',
    'SelectPdf/SelectPdf.csproj',
    'IronPdfExample/IronPdfExample.csproj',
    'Syncfusion/Syncfusion.csproj',
    'SpirePdf/SpirePdf.csproj'
  )
}

if (-not $PdfCount -or $PdfCount -lt 1) {
  $PdfCount = Read-PdfCount
}

Write-Host "Building all projects ($Configuration)..." -ForegroundColor Cyan
dotnet build "$PSScriptRoot/nreco.sln" -c $Configuration
if ($LASTEXITCODE -ne 0) {
  Write-Host "Build failed!" -ForegroundColor Red
  exit 1
}

# Prepare logs directory with timestamp + correlation
$logRoot = Join-Path $PSScriptRoot 'logs'
$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$runGuid = [guid]::NewGuid().ToString('N').Substring(0,8)
$runIdFull = "$runId-$runGuid"
$runLogDir = Join-Path $logRoot $runIdFull
New-Item -ItemType Directory -Force -Path $runLogDir | Out-Null

# Prepare artifacts directory with timestamp + correlation
$artifactRoot = Join-Path $PSScriptRoot 'artifacts'
$runArtifactDir = Join-Path $artifactRoot $runIdFull
New-Item -ItemType Directory -Force -Path $runArtifactDir | Out-Null

Write-Host ("Run correlation: {0}  Logs: {1}  Artifacts: {2}" -f $runGuid, $runLogDir, $runArtifactDir) -ForegroundColor DarkGray

function Invoke-ProjectSequential {
  param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][int]$PdfCount,
    [Parameter(Mandatory)][string]$Configuration,
    [Parameter(Mandatory)][string]$LogDir,
    [Parameter(Mandatory)][string]$ArtifactDir
  )

  $projFile = Split-Path -Path $ProjectPath -Leaf
  $projName = [System.IO.Path]::GetFileNameWithoutExtension($projFile)

  Write-Host "`nRunning $projName (sequential: $PdfCount PDFs)..." -ForegroundColor Green
  $sw = [System.Diagnostics.Stopwatch]::StartNew()

  # Prepare output directory for this project
  $projArtifactDir = Join-Path $ArtifactDir $projName
  New-Item -ItemType Directory -Force -Path $projArtifactDir | Out-Null

  # Prepare log file
  $outLog = Join-Path $LogDir ("{0}-sequential.out.log" -f $projName)
  $errLog = Join-Path $LogDir ("{0}-sequential.err.log" -f $projName)

  # Generate output paths for sequential processing
  $outputPaths = @()
  for ($i = 1; $i -le $PdfCount; $i++) {
    $outputPaths += Join-Path $projArtifactDir ("{0}-{1:D2}.pdf" -f $projName, $i)
  }

  # Run the project with sequential mode
  $args = @('run', '--no-build', '--configuration', $Configuration, '--project', $ProjectPath, '--sequential') + $outputPaths

  $header = ("[runner] proj={0} mode=sequential pdfCount={1} runCorr={2} started={3:O}" -f $projName, $PdfCount, $runGuid, (Get-Date))
  Add-Content -Path $outLog -Value $header
  Add-Content -Path $errLog -Value $header

  $process = Start-Process `
    -FilePath 'dotnet' `
    -ArgumentList $args `
    -WorkingDirectory $PSScriptRoot `
    -PassThru `
    -NoNewWindow `
    -RedirectStandardOutput $outLog `
    -RedirectStandardError $errLog

  # Wait for process with timeout
  $process.WaitForExit(($PerProcessTimeoutSec * 1000)) | Out-Null

  $runtimeSec = $sw.Elapsed.TotalSeconds
  $exit = $process.ExitCode
  $cpuSec = $process.TotalProcessorTime.TotalSeconds

  # Check for success signature
  $hadSuccess = $false
  if (Test-Path $outLog) {
    $successCount = (Select-String -Path $outLog -Pattern 'PDF saved to:' -AllMatches).Matches.Count
    $hadSuccess = ($successCount -eq $PdfCount)
    if ($successCount -gt 0) {
      Write-Host ("  -> Success signatures found: {0}/{1}" -f $successCount, $PdfCount) -ForegroundColor Green
    }
  }

  # Artifact presence check
  $generatedCount = 0
  foreach ($outputPath in $outputPaths) {
    if (Test-Path $outputPath) {
      $generatedCount++
    }
  }

  Write-Host ("  -> Process exited code {0} after {1:N1}s (cpu {2:N1}s, success={3}, artifacts={4}/{5})" -f $exit, $runtimeSec, $cpuSec, $hadSuccess, $generatedCount, $PdfCount) -ForegroundColor Cyan

  if ($generatedCount -lt $PdfCount) {
    $missingCount = $PdfCount - $generatedCount
    Write-Host ("  -> Missing {0} artifacts out of {1} expected" -f $missingCount, $PdfCount) -ForegroundColor Yellow
    if (Test-Path $errLog) {
      Add-Content -Path $errLog -Value ("Missing {0} artifacts out of {1} expected" -f $missingCount, $PdfCount)
    }
  }

  $sw.Stop()

  $success = if ($hadSuccess -and $generatedCount -eq $PdfCount) { 1 } else { 0 }
  $failed = if ($success -eq 1) { 0 } else { 1 }
  $elapsed = $sw.Elapsed

  Write-Host ("Completed {0} in {1} ({2} seconds). Success: {3}, Failed: {4}" -f `
    $projName, $elapsed.ToString('hh\:mm\:ss\.fff'), [math]::Round($elapsed.TotalSeconds,3), $success, $failed) -ForegroundColor Yellow

  if ($failed -gt 0) {
    Write-Host ("  -> See logs: {0}" -f $LogDir) -ForegroundColor Magenta
  }
}

# Iterate projects in order, run each with sequential PDF generation
foreach ($proj in $Projects) {
  $fullProjPath = Join-Path $PSScriptRoot $proj
  if (-not (Test-Path $fullProjPath)) {
    Write-Host ("Skipping missing project: {0}" -f $proj) -ForegroundColor DarkYellow
    continue
  }

  $projName = [System.IO.Path]::GetFileNameWithoutExtension($proj)
  $projLogDir = Join-Path $runLogDir $projName
  New-Item -ItemType Directory -Force -Path $projLogDir | Out-Null

  # Warmup PuppeteerSharp once to pre-download Chromium and warm caches
  if ($projName -eq 'PuppeteerSharp') {
    Write-Host "Warming up PuppeteerSharp (pre-download Chromium)..." -ForegroundColor DarkCyan
    $warmOutLog = Join-Path $projLogDir ("{0}-warmup.out.log" -f $projName)
    $warmErrLog = Join-Path $projLogDir ("{0}-warmup.err.log" -f $projName)
    $warmPdf    = Join-Path $runArtifactDir ("{0}-warmup.pdf" -f $projName)
    $warmArgs   = @('run','--no-build','--configuration',$Configuration,'--project',$fullProjPath,$warmPdf)
    $warmProc = Start-Process -FilePath 'dotnet' -ArgumentList $warmArgs -WorkingDirectory $PSScriptRoot -PassThru -Wait -NoNewWindow -RedirectStandardOutput $warmOutLog -RedirectStandardError $warmErrLog
    if ($warmProc.ExitCode -ne 0) {
      Write-Host ("Warmup exited with code {0}. See {1}" -f $warmProc.ExitCode, $warmErrLog) -ForegroundColor Yellow
    } else {
      Write-Host "Warmup complete." -ForegroundColor DarkCyan
    }
  }

  Invoke-ProjectSequential -ProjectPath $fullProjPath -PdfCount $PdfCount -Configuration $Configuration -LogDir $projLogDir -ArtifactDir $runArtifactDir
}

Write-Host "`nAll projects completed! Logs: $runLogDir  Artifacts: $runArtifactDir" -ForegroundColor Cyan
