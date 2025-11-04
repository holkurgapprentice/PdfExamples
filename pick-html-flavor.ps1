#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Action {
  while ($true) {
    Write-Host ""
    Write-Host "Select action:" -ForegroundColor Cyan
    Write-Host "  [F] Apply HTML flavor (copy class/inline into assets)"
    Write-Host "  [M] Move result PDFs from project folders to repo root"
    $c = Read-Host "Enter F or M"
    if ([string]::IsNullOrWhiteSpace($c)) { continue }
    switch ($c.Trim().Substring(0,1).ToUpperInvariant()) {
      'F' { return 'flavor' }
      'M' { return 'move' }
      default { Write-Warning "Invalid choice. Please enter F or M." }
    }
  }
}

function Get-FlavorChoice {
  while ($true) {
    Write-Host ""
    Write-Host "Pick flavor naming:" -ForegroundColor Cyan
    Write-Host "  [C] class"
    Write-Host "  [I] inline"
    $inputChar = Read-Host "Enter C or I"
    if ([string]::IsNullOrWhiteSpace($inputChar)) { continue }
    switch ($inputChar.Trim().Substring(0,1).ToUpperInvariant()) {
      'C' { return 'class' }
      'I' { return 'inline' }
      default { Write-Warning "Invalid choice. Please enter C or I." }
    }
  }
}

function Apply-Flavors {
  param(
    [Parameter(Mandatory=$true)][ValidateSet('class','inline')] [string] $Flavor
  )

  $scriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
  $srcDir = Join-Path $scriptRoot 'assets\html-flavors'
  $dstDir = Join-Path $scriptRoot 'assets'

  if (-not (Test-Path $srcDir)) { throw "Source directory not found: $srcDir" }
  if (-not (Test-Path $dstDir)) { throw "Destination directory not found: $dstDir" }

  Write-Host ""
  Write-Host "Applying '$Flavor' flavor to assets..." -ForegroundColor Green

  $names = @('header','footer','main')
  $copied = @()

  foreach ($name in $names) {
    $srcFile = Join-Path $srcDir ("{0}.{1}.html" -f $name, $Flavor)
    $dstFile = Join-Path $dstDir ("{0}.html" -f $name)

    if (-not (Test-Path $srcFile)) {
      throw "Expected source file not found: $srcFile"
    }

    if (Test-Path $dstFile) {
      Write-Host ("Removing existing {0}" -f (Resolve-Path $dstFile)) -ForegroundColor DarkYellow
      Remove-Item -LiteralPath $dstFile -Force
    }

    Copy-Item -LiteralPath $srcFile -Destination $dstFile -Force
    $copied += [PSCustomObject]@{ From=$srcFile; To=$dstFile }
  }

  Write-Host ""
  Write-Host "Done copying flavor files:" -ForegroundColor Green
  $copied | ForEach-Object { Write-Host ("  {0} -> {1}" -f $_.From, $_.To) }
}

function Move-Results {
  param(
    [Parameter(Mandatory=$true)][ValidateSet('class','inline')] [string] $FlavorSuffix
  )

  $scriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
  # Detect project folders by presence of a .csproj inside immediate subdirectories
  $projectDirs = Get-ChildItem -Path $scriptRoot -Directory | Where-Object {
    (Get-ChildItem -Path $_.FullName -Filter *.csproj -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0
  }

  if (-not $projectDirs) {
    Write-Warning "No project folders with .csproj found under $scriptRoot. Nothing to move."
    return
  }

  $moved = @()
  foreach ($dir in $projectDirs) {
    $projectName = $dir.Name
    $projectPath = $dir.FullName

    # Prefer 'output-{projectName}.pdf', but fall back to 'output.pdf' if needed
    $candidates = @("output-$projectName.pdf", "output.pdf")
    $sourcePath = $null
    foreach ($cand in $candidates) {
      $try = Join-Path $projectPath $cand
      if (Test-Path $try) { $sourcePath = $try; break }
    }

    if (-not $sourcePath) {
      Write-Host ("[{0}] No output PDF found (looked for {1}). Skipping." -f $projectName, ($candidates -join ', ')) -ForegroundColor DarkYellow
      continue
    }

    $destFile = "output-{0}-{1}.pdf" -f $projectName, $FlavorSuffix
    $destPath = Join-Path $scriptRoot $destFile

    if (Test-Path $destPath) {
      Write-Host ("Removing existing destination {0}" -f (Resolve-Path $destPath)) -ForegroundColor DarkYellow
      Remove-Item -LiteralPath $destPath -Force
    }

    Move-Item -LiteralPath $sourcePath -Destination $destPath -Force
    $moved += [PSCustomObject]@{ Project=$projectName; From=$sourcePath; To=$destPath }
  }

  if ($moved.Count -gt 0) {
    Write-Host ""
    Write-Host "Moved PDFs to repo root with suffix '-$FlavorSuffix':" -ForegroundColor Green
    $moved | ForEach-Object { Write-Host ("  [{0}] {1} -> {2}" -f $_.Project, $_.From, $_.To) }
  } else {
    Write-Host "No PDFs moved." -ForegroundColor Yellow
  }
}

try {
  $action = Get-Action
  switch ($action) {
    'flavor' {
      $choice = Get-FlavorChoice
      Apply-Flavors -Flavor $choice
    }
    'move' {
      $choice = Get-FlavorChoice   # use same naming prompt (i/c) for suffix
      Move-Results -FlavorSuffix $choice
    }
  }

  exit 0
}
catch {
  Write-Error $_
  exit 1
}