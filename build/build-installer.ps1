#requires -Version 5.1
<#
.SYNOPSIS
    Full release pipeline for PC Power Monitor: test -> publish -> Inno Setup installer.

.DESCRIPTION
    1. Runs `dotnet test` on the solution. If ANY test fails the script stops and
       NO installer is produced.
    2. Calls build\publish.ps1 -Version <Version> (self-contained single-file win-x64).
    3. Locates ISCC.exe (per-user install, Program Files (x86), or PATH).
    4. Compiles build\installer.iss with /DAppVersion=<Version>.
    5. Reports the final dist\PcPowerMonitorSetup-<Version>.exe path + size and
       warns if it exceeds 150 MB.

.EXAMPLE
    pwsh apps/pc-power-monitor/build/build-installer.ps1 -Version 1.0.0
#>
[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [string]$DotnetPath = "C:\Program Files\dotnet\dotnet.exe"
)

$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$appRoot   = Split-Path -Parent $scriptDir
$sln       = Join-Path $appRoot "PcPowerMonitor.sln"
$iss       = Join-Path $scriptDir "installer.iss"
$distDir   = Join-Path $appRoot "dist"
$setupExe  = Join-Path $distDir ("PcPowerMonitorSetup-{0}.exe" -f $Version)

function Resolve-Dotnet {
    if (Test-Path $DotnetPath) { return $DotnetPath }
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw "dotnet not found at '$DotnetPath' and not on PATH."
}

function Resolve-Iscc {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path $c)) { return $c }
    }
    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw "ISCC.exe not found. Install Inno Setup 6 from https://jrsoftware.org/isdl.php"
}

try {
    $dotnet = Resolve-Dotnet
    Write-Host "==> dotnet : $dotnet"
    Write-Host "==> version: $Version"

    # --- 1. Tests must pass ------------------------------------------------
    Write-Host ""
    Write-Host "==> [1/4] dotnet test $sln"
    & $dotnet test $sln --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed (exit $LASTEXITCODE). Stopping - NO installer produced."
        exit 1
    }

    # --- 2. Publish --------------------------------------------------------
    Write-Host ""
    Write-Host "==> [2/4] publish.ps1"
    & (Join-Path $scriptDir "publish.ps1") -Version $Version -DotnetPath $DotnetPath
    if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
        throw "publish.ps1 failed (exit $LASTEXITCODE)."
    }

    # --- 3. Locate ISCC -------------------------------------------------
    Write-Host ""
    Write-Host "==> [3/4] locating ISCC.exe"
    $iscc = Resolve-Iscc
    Write-Host "    ISCC: $iscc"

    # --- 4. Compile installer -------------------------------------------
    Write-Host ""
    Write-Host "==> [4/4] compiling installer"
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
    & $iscc "/DAppVersion=$Version" "/O$distDir" $iss
    if ($LASTEXITCODE -ne 0) {
        throw "ISCC.exe failed (exit $LASTEXITCODE)."
    }

    if (-not (Test-Path $setupExe)) {
        throw "Installer not found after compile: $setupExe"
    }

    $sizeMb = [math]::Round((Get-Item $setupExe).Length / 1MB, 2)
    Write-Host ""
    Write-Host "=========================================================="
    Write-Host " Installer OK"
    Write-Host "   file: $setupExe"
    Write-Host "   size: $sizeMb MB"
    if ($sizeMb -gt 150) {
        Write-Warning "Installer exceeds 150 MB (phase-10 non-functional target)."
    }
    Write-Host "=========================================================="
}
catch {
    Write-Error ("build-installer failed: " + $_.Exception.Message)
    exit 1
}
