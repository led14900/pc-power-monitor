#requires -Version 5.1
<#
.SYNOPSIS
    Publish PC Power Monitor as a self-contained single-file win-x64 build and stage
    the user-facing docs next to the executable.

.DESCRIPTION
    Runs `dotnet publish` for src/PcPowerMonitor.App with:
      -r win-x64 --self-contained true
      -p:PublishSingleFile=true
      -p:IncludeNativeLibrariesForSelfExtract=true   (LHM .sys + e_sqlite3.dll)
      -p:PublishReadyToRun=true
      -p:PublishTrimmed=false                        (WPF/LHM/ScottPlot use reflection)
    Output goes to apps/pc-power-monitor/publish/win-x64/ then README / notices /
    LICENSE are copied in.

.NOTES
    The self-contained / single-file switches are passed here (not in the .csproj) so
    a plain `dotnet build` / `dotnet test` is not forced into a RID-specific restore.
#>
[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [string]$DotnetPath = "C:\Program Files\dotnet\dotnet.exe",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

# --- Resolve paths ---------------------------------------------------------
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$appRoot   = Split-Path -Parent $scriptDir                       # apps/pc-power-monitor
$proj      = Join-Path $appRoot "src\PcPowerMonitor.App\PcPowerMonitor.App.csproj"
$outDir    = Join-Path $appRoot ("publish\" + $Runtime)
$docsDir   = Join-Path $appRoot "docs"

if (-not (Test-Path $proj)) { throw "Project not found: $proj" }

$dotnet = $DotnetPath
if (-not (Test-Path $dotnet)) {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $cmd) { throw "dotnet not found at '$DotnetPath' and not on PATH." }
    $dotnet = $cmd.Source
}

Write-Host "==> dotnet : $dotnet"
Write-Host "==> project: $proj"
Write-Host "==> output : $outDir"
Write-Host "==> version: $Version"

# --- Clean previous output ---------------------------------------------------
if (Test-Path $outDir) {
    Write-Host "==> removing old output"
    Remove-Item $outDir -Recurse -Force
}
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

# --- Publish ---------------------------------------------------------------
$publishArgs = @(
    "publish", $proj,
    "-c", "Release",
    "-r", $Runtime,
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:PublishReadyToRun=true",
    "-p:PublishTrimmed=false",
    "-p:DebugType=none",
    "-p:DebugSymbols=false",
    "-p:Version=$Version",
    "-p:FileVersion=$Version.0",
    "-p:AssemblyVersion=$Version.0",
    "-o", $outDir
)

Write-Host "==> $dotnet $($publishArgs -join ' ')"
& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed (exit $LASTEXITCODE)."
}

# --- Stage user docs -------------------------------------------------------
$payload = @(
    @{ Src = (Join-Path $docsDir "README-nguoi-dung.md");   Dst = "README-nguoi-dung.md" },
    @{ Src = (Join-Path $docsDir "THIRD-PARTY-NOTICES.txt"); Dst = "THIRD-PARTY-NOTICES.txt" },
    @{ Src = (Join-Path $appRoot "LICENSE");                 Dst = "LICENSE" }
)
foreach ($item in $payload) {
    if (-not (Test-Path $item.Src)) { throw "Missing bundled file: $($item.Src)" }
    Copy-Item $item.Src (Join-Path $outDir $item.Dst) -Force
    Write-Host "==> copied $($item.Dst)"
}

# --- Report --------------------------------------------------------------
$exe = Join-Path $outDir "PcPowerMonitor.App.exe"
if (-not (Test-Path $exe)) { throw "Published exe not found: $exe" }

$exeMb   = [math]::Round((Get-Item $exe).Length / 1MB, 2)
$totalMb = [math]::Round(((Get-ChildItem $outDir -Recurse -File | Measure-Object Length -Sum).Sum) / 1MB, 2)

Write-Host ""
Write-Host "=========================================================="
Write-Host " Publish OK"
Write-Host "   exe         : $exe"
Write-Host "   exe size    : $exeMb MB"
Write-Host "   folder size : $totalMb MB"
Write-Host "=========================================================="
