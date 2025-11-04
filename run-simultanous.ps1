#!/usr/bin/env pwsh
# Parallel runner: runs each project N times concurrently, times completion per project, then moves to the next.
# Usage examples:
#   pwsh ./run-multi.ps1               # prompts for instance count, builds Release, runs all projects
#   pwsh ./run-multi.ps1 -Instances 10 # runs 10 parallel instances per project
#   pwsh ./run-multi.ps1 -Instances 5 -Configuration Debug
#   pwsh ./run-multi.ps1 -Instances 8 -Projects @('NReco/NReco.csproj','PuppeteerSharp/PuppeteerSharp.csproj')

[CmdletBinding()]
param(
  [int]$Instances,
  [ValidateSet('Debug','Release')]
  [string]$Configuration = 'Release',
  [string[]]$Projects,
  [int]$PerProcessTimeoutSec = 120,
  [int]$PollIntervalMs = 500
)

$ErrorActionPreference = 'Stop'

function Read-Instances([string]$Prompt = 'Enter number of parallel instances per project') {
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

if (-not $Instances -or $Instances -lt 1) {
  $Instances = Read-Instances
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

# Per-project timeout overrides (seconds)
$ProjectTimeouts = @{
  'PuppeteerSharp' = 60
}

function Invoke-ProjectParallel {
  param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][int]$InstanceCount,
    [Parameter(Mandatory)][string]$Configuration,
    [Parameter(Mandatory)][string]$LogDir,
    [Parameter(Mandatory)][string]$ArtifactDir
  )

  $projFile = Split-Path -Path $ProjectPath -Leaf
  $projName = [System.IO.Path]::GetFileNameWithoutExtension($projFile)

  # Determine effective timeout (allow per-project override)
  $effectiveTimeout = $PerProcessTimeoutSec
  if ($script:ProjectTimeouts -and $script:ProjectTimeouts.ContainsKey($projName)) {
    $effectiveTimeout = [int]$script:ProjectTimeouts[$projName]
  }

  Write-Host "`nRunning $projName ($InstanceCount instances)..." -ForegroundColor Green
  $sw = [System.Diagnostics.Stopwatch]::StartNew()

  $processes = @()
  $startTimes = @{}
  $pidToErrLog = @{}
  $pidToOutLog = @{}
  $pidToIndex = @{}
  $pidToCorr = @{}
  $graceUntil = @{}
  $lastStatusTime = Get-Date
  $successSeen = @{}
  $reportedExit = @{}
  $lastLogWrite = @{}
  $idleThresholdSec = 10
  $pidToOutFile = @{}
  $indexToOutFile = @{}
  for ($i = 1; $i -le $InstanceCount; $i++) {
    $outLog = Join-Path $LogDir ("{0}-{1:D2}.out.log" -f $projName, $i)
    $errLog = Join-Path $LogDir ("{0}-{1:D2}.err.log" -f $projName, $i)

    $outFile = Join-Path $ArtifactDir ("{0}-{1:D2}.pdf" -f $projName, $i)
    $args = @('run', '--no-build', '--configuration', $Configuration, '--project', $ProjectPath, $outFile)

    $p = Start-Process `
      -FilePath 'dotnet' `
      -ArgumentList $args `
      -WorkingDirectory $PSScriptRoot `
      -PassThru `
      -NoNewWindow `
      -RedirectStandardOutput $outLog `
      -RedirectStandardError $errLog
    $pidToErrLog[$p.Id] = $errLog
    $pidToOutLog[$p.Id] = $outLog
    $pidToIndex[$p.Id] = $i
    $pidToCorr[$p.Id] = ([guid]::NewGuid().ToString('N').Substring(0,8))
    $pidToOutFile[$p.Id] = $outFile
    $indexToOutFile[$i] = $outFile

    $header = ("[runner] proj={0} inst={1:D2} pid={2} corr={3} runCorr={4} started={5:O}" -f $projName, $i, $p.Id, $pidToCorr[$p.Id], $runGuid, (Get-Date))
    Add-Content -Path $outLog -Value $header
    Add-Content -Path $errLog -Value $header

    $startTimes[$p.Id] = Get-Date
    $lastLogWrite[$p.Id] = Get-Date
    $processes += $p
  }

  # Wait for all processes to complete with timeout and progress
  while (($processes | Where-Object { -not $_.HasExited }).Count -gt 0) {
    foreach ($p in $processes) {
      if ($p) { $p.Refresh() }

      # Report exit once with details
      if ($p -and $p.HasExited -and (-not $reportedExit.ContainsKey($p.Id))) {
        $runtimeSec = ((Get-Date) - $startTimes[$p.Id]).TotalSeconds
        $exit = $p.ExitCode
        $cpuSec = $p.TotalProcessorTime.TotalSeconds
        $hadSuccess = $false
        if ($pidToOutLog.ContainsKey($p.Id)) {
          $outPath = $pidToOutLog[$p.Id]
          if ((Test-Path $outPath) -and (Select-String -Path $outPath -Pattern 'PDF saved to:' -Quiet)) {
            $hadSuccess = $true
          }
        }
        $inst = if ($pidToIndex.ContainsKey($p.Id)) { $pidToIndex[$p.Id] } else { -1 }
        $corr = if ($pidToCorr.ContainsKey($p.Id)) { $pidToCorr[$p.Id] } else { "n/a" }

        # Artifact presence check
        $artifactPath = $null
        if ($pidToOutFile.ContainsKey($p.Id)) {
          $artifactPath = $pidToOutFile[$p.Id]
        } elseif ($inst -gt 0) {
          $artifactPath = Join-Path $ArtifactDir ("{0}-{1:D2}.pdf" -f $projName, $inst)
        }
        $artifactExists = ($artifactPath -and (Test-Path $artifactPath))

        Write-Host ("  -> PID {0}[inst={1:D2}] exited code {2} after {3:N1}s (cpu {4:N1}s, successSig={5}, corr={6}, artifact={7})" -f $p.Id, [int]$inst, $exit, $runtimeSec, $cpuSec, $hadSuccess, $corr, $artifactExists) -ForegroundColor Cyan

        if (-not $artifactExists) {
          $warn = ("Artifact missing for inst={0:D2} expected={1}" -f [int]$inst, $artifactPath)
          Write-Host ("  -> {0}" -f $warn) -ForegroundColor Yellow
          if ($pidToErrLog.ContainsKey($p.Id)) {
            Add-Content -Path $pidToErrLog[$p.Id] -Value $warn
          }
        }

        $reportedExit[$p.Id] = $true
        continue
      }

      if ($p -and -not $p.HasExited) {
        $ageSec = ((Get-Date) - $startTimes[$p.Id]).TotalSeconds

        # Track last log activity
        $lastWrite = $null
        if ($pidToOutLog.ContainsKey($p.Id)) {
          $outPath = $pidToOutLog[$p.Id]
          if (Test-Path $outPath) { $lastWrite = (Get-Item $outPath).LastWriteTime }
        }
        if ($pidToErrLog.ContainsKey($p.Id)) {
          $errPath = $pidToErrLog[$p.Id]
          if (Test-Path $errPath) {
            $lw = (Get-Item $errPath).LastWriteTime
            if (-not $lastWrite -or $lw -gt $lastWrite) { $lastWrite = $lw }
          }
        }
        if ($lastWrite) { $lastLogWrite[$p.Id] = $lastWrite }
        $idleSec = if ($lastLogWrite.ContainsKey($p.Id)) { ((Get-Date) - $lastLogWrite[$p.Id]).TotalSeconds } else { $ageSec }

        # Observe success signature early
        $hasSuccessSignature = $false
        if ($pidToOutLog.ContainsKey($p.Id)) {
          $outPath = $pidToOutLog[$p.Id]
          if ((Test-Path $outPath) -and (Select-String -Path $outPath -Pattern 'PDF saved to:' -Quiet)) {
            $hasSuccessSignature = $true
            $successSeen[$p.Id] = $true
          }
        }

        # If timeout reached, give grace if success signature observed
        if ($ageSec -ge $effectiveTimeout) {
          if ($hasSuccessSignature) {
            if (-not $graceUntil.ContainsKey($p.Id)) {
              $graceUntil[$p.Id] = (Get-Date).AddSeconds(2)
              Write-Host ("  -> PID {0} reported success; granting 2s grace before considering kill" -f $p.Id) -ForegroundColor DarkYellow
            }
            elseif ((Get-Date) -lt $graceUntil[$p.Id]) {
              continue
            }
          }

          try {
            # Final refresh and short wait before kill to avoid racing a just-terminated process
            $p.Refresh()
            if (-not $p.HasExited) {
              $null = $p.WaitForExit(500)
              $p.Refresh()
            }
            if (-not $p.HasExited) {
              $inst = if ($pidToIndex.ContainsKey($p.Id)) { $pidToIndex[$p.Id] } else { -1 }
              $corr = if ($pidToCorr.ContainsKey($p.Id)) { $pidToCorr[$p.Id] } else { "n/a" }
              Write-Host ("  -> Killing timed-out PID {0}[inst={1:D2}] (age {2:N1}s, successSig={3}, idle {4:N1}s, corr={5})" -f $p.Id, [int]$inst, $ageSec, $hasSuccessSignature, $idleSec, $corr) -ForegroundColor DarkRed
              $p.Kill()
              $note = ("Killed by timeout after {0:N1}s (inst={1:D2}, successSig={2}, idle {3:N1}s, corr={4}) at {5:O}" -f $ageSec, [int]$inst, $hasSuccessSignature, $idleSec, $corr, (Get-Date))
              if ($pidToErrLog.ContainsKey($p.Id)) {
                Add-Content -Path $pidToErrLog[$p.Id] -Value $note
              }
            }
          } catch {}
        }
      }
    }

    # Periodic status of in-flight PIDs + aggregate stats
    if (((Get-Date) - $lastStatusTime).TotalSeconds -ge 2) {
      $active = $processes | Where-Object { -not $_.HasExited }
      if ($active.Count -gt 0) {
        $activePidLabels = @()
        foreach ($ap in $active) {
          $idx = if ($pidToIndex.ContainsKey($ap.Id)) { $pidToIndex[$ap.Id] } else { -1 }
          $activePidLabels += ("{0}[{1:D2}]" -f $ap.Id, [int]$idx)
        }
        Write-Host ("  -> In-flight PIDs ({0}): {1}" -f $active.Count, ($activePidLabels -join ', '))

        $now = Get-Date
        $activeSuccess = ($active | Where-Object { $successSeen.ContainsKey($_.Id) }).Count
        $exitedOk = ($processes | Where-Object { $_.HasExited -and $_.ExitCode -eq 0 }).Count
        $exitedFail = ($processes | Where-Object { $_.HasExited -and $_.ExitCode -ne 0 }).Count
        $idleCount = 0
        foreach ($ap in $active) {
          $idle = if ($lastLogWrite.ContainsKey($ap.Id)) { ($now - $lastLogWrite[$ap.Id]).TotalSeconds } else { 1e9 }
          if ($idle -ge $idleThresholdSec) { $idleCount++ }
        }
        Write-Host ("  -> Status: active={0} (successSigSeen={1}, idle>={2}s={3}) exitedOK={4}, exitedFail={5}" -f $active.Count, $activeSuccess, $idleThresholdSec, $idleCount, $exitedOk, $exitedFail) -ForegroundColor Gray
      }
      $lastStatusTime = Get-Date
    }

    Start-Sleep -Milliseconds $PollIntervalMs
  }

  $sw.Stop()

  $success = ($processes | Where-Object { $_.HasExited -and $_.ExitCode -eq 0 }).Count
  $failed  = $InstanceCount - $success
  $elapsed = $sw.Elapsed

  # Final artifact audit
  $missingInst = @()
  for ($i = 1; $i -le $InstanceCount; $i++) {
    $expected = if ($indexToOutFile.ContainsKey($i)) { $indexToOutFile[$i] } else { Join-Path $ArtifactDir ("{0}-{1:D2}.pdf" -f $projName, $i) }
    if (-not (Test-Path $expected)) { $missingInst += $i }
  }
  if ($missingInst.Count -gt 0) {
    Write-Host ("  -> Artifact audit: missing {0} of {1}. Instances: {2}" -f $missingInst.Count, $InstanceCount, ($missingInst -join ', ')) -ForegroundColor Magenta
  }

  Write-Host ("Completed {0} in {1} ({2} seconds). Success: {3}, Failed: {4}" -f `
    $projName, $elapsed.ToString('hh\:mm\:ss\.fff'), [math]::Round($elapsed.TotalSeconds,3), $success, $failed) -ForegroundColor Yellow

  if ($failed -gt 0 -or $missingInst.Count -gt 0) {
    Write-Host ("  -> See logs: {0}" -f $LogDir) -ForegroundColor Magenta
  }
}

# Iterate projects in order, run each N times in parallel, wait, report, then continue to next
foreach ($proj in $Projects) {
  $fullProjPath = Join-Path $PSScriptRoot $proj
  if (-not (Test-Path $fullProjPath)) {
    Write-Host ("Skipping missing project: {0}" -f $proj) -ForegroundColor DarkYellow
    continue
  }

  $projName = [System.IO.Path]::GetFileNameWithoutExtension($proj)
  $projLogDir = Join-Path $runLogDir $projName
  New-Item -ItemType Directory -Force -Path $projLogDir | Out-Null
  $projArtifactDir = Join-Path $runArtifactDir $projName
  New-Item -ItemType Directory -Force -Path $projArtifactDir | Out-Null

  # Warmup PuppeteerSharp once to pre-download Chromium and warm caches
  if ($projName -eq 'PuppeteerSharp') {
    Write-Host "Warming up PuppeteerSharp (pre-download Chromium)..." -ForegroundColor DarkCyan
    $warmOutLog = Join-Path $projLogDir ("{0}-warmup.out.log" -f $projName)
    $warmErrLog = Join-Path $projLogDir ("{0}-warmup.err.log" -f $projName)
    $warmPdf    = Join-Path $projArtifactDir ("{0}-warmup.pdf" -f $projName)
    $warmArgs   = @('run','--no-build','--configuration',$Configuration,'--project',$fullProjPath,$warmPdf)
    $warmProc = Start-Process -FilePath 'dotnet' -ArgumentList $warmArgs -WorkingDirectory $PSScriptRoot -PassThru -Wait -NoNewWindow -RedirectStandardOutput $warmOutLog -RedirectStandardError $warmErrLog
    if ($warmProc.ExitCode -ne 0) {
      Write-Host ("Warmup exited with code {0}. See {1}" -f $warmProc.ExitCode, $warmErrLog) -ForegroundColor Yellow
    } else {
      Write-Host "Warmup complete." -ForegroundColor DarkCyan
    }
  }

  Invoke-ProjectParallel -ProjectPath $fullProjPath -InstanceCount $Instances -Configuration $Configuration -LogDir $projLogDir -ArtifactDir $projArtifactDir
}

Write-Host "`nAll projects completed! Logs: $runLogDir  Artifacts: $runArtifactDir" -ForegroundColor Cyan
