<#
.SYNOPSIS
    Removes the Crabtya mod loader from an Everything is Crab game directory.

.DESCRIPTION
    Deletes only the files and directories that the Crabtya loader added during install.
    The game executable, game data, and original game DLLs are never removed.

    By default the Mods/ directory is preserved so your installed mods survive the
    uninstall.  Pass -RemoveMods to delete it as well.

.PARAMETER GamePath
    Full path to the Everything is Crab game directory (the folder that contains
    "Everything is Crab.exe").

.PARAMETER RemoveMods
    If specified, also removes the Mods/ directory and all mod data inside it.
    Use with caution — this is irreversible.

.PARAMETER Force
    Suppresses the confirmation prompt before removing files.

.EXAMPLE
    .\Uninstall-Crabtya.ps1 -GamePath "C:\Games\Everything is Crab"

.EXAMPLE
    .\Uninstall-Crabtya.ps1 -GamePath "C:\Games\Everything is Crab" -RemoveMods -Force
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = "High")]
param(
    [Parameter(Mandatory = $true)]
    [string]$GamePath,

    [switch]$RemoveMods,

    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Write-Step {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "  [WARN] $Message" -ForegroundColor Yellow
}

function Write-Skip {
    param([string]$Message)
    Write-Host "  [--] $Message" -ForegroundColor DarkGray
}

function Remove-IfPresent {
    param(
        [string]$Path,
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path))
    {
        Write-Skip "$Label not found (already removed or never installed)."
        return
    }

    if ($PSCmdlet.ShouldProcess($Path, "Remove $Label"))
    {
        Remove-Item -LiteralPath $Path -Recurse -Force
        Write-Ok "Removed $Label."
    }
}

# ---------------------------------------------------------------------------
# Validate game path
# ---------------------------------------------------------------------------

$GamePath = [System.IO.Path]::GetFullPath($GamePath)

if (-not (Test-Path -LiteralPath $GamePath -PathType Container))
{
    Write-Host "  [FAIL] Game path does not exist: $GamePath" -ForegroundColor Red
    exit 1
}

$gameExe = Join-Path $GamePath "Everything is Crab.exe"
if (-not (Test-Path -LiteralPath $gameExe -PathType Leaf))
{
    Write-Host "  [WARN] 'Everything is Crab.exe' not found in: $GamePath" -ForegroundColor Yellow
    Write-Host "  [WARN] Proceeding anyway — ensure the game is not running before uninstalling." -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# Confirm
# ---------------------------------------------------------------------------

Write-Host ""
Write-Host "Crabtya Loader Uninstaller" -ForegroundColor White
Write-Host "  Game path : $GamePath"
if ($RemoveMods) { Write-Host "  Mods/     : will be REMOVED" -ForegroundColor Yellow }
else             { Write-Host "  Mods/     : will be preserved" -ForegroundColor DarkGray }
Write-Host ""

if (-not $Force -and -not $PSCmdlet.ShouldContinue(
    "This will remove the Crabtya loader files from the game directory.",
    "Confirm Crabtya Uninstall"))
{
    Write-Host "Uninstall cancelled."
    exit 0
}

# ---------------------------------------------------------------------------
# Remove loader-owned files
# These are the items that Install-Crabtya.ps1 / the loader zip adds.
# Game-original files are NOT listed here and are never touched.
# ---------------------------------------------------------------------------

Write-Step "Removing loader files..."

# BepInEx directory (entire — contains loader plugin, interop, core libs, config, logs).
Remove-IfPresent -Path (Join-Path $GamePath "BepInEx") -Label "BepInEx/"

# .NET runtime shipped with the loader.
Remove-IfPresent -Path (Join-Path $GamePath "dotnet") -Label "dotnet/"

# Doorstop shim files.
Remove-IfPresent -Path (Join-Path $GamePath "winhttp.dll") -Label "winhttp.dll"
Remove-IfPresent -Path (Join-Path $GamePath "doorstop_config.ini") -Label "doorstop_config.ini"
Remove-IfPresent -Path (Join-Path $GamePath ".doorstop_version") -Label ".doorstop_version"
Remove-IfPresent -Path (Join-Path $GamePath "changelog.txt") -Label "changelog.txt"

# Loader bundle README (if present).
Remove-IfPresent -Path (Join-Path $GamePath "README.txt") -Label "README.txt"

# Crabtya isolated save data (modded session sandbox — does not contain base-game saves).
$isolatedSave = Join-Path $GamePath "CrabtyaData"
if (Test-Path -LiteralPath $isolatedSave)
{
    Write-Host ""
    Write-Warn "Found modded save sandbox: $isolatedSave"
    Write-Warn "This directory contains save data from modded sessions ONLY and is separate"
    Write-Warn "from the base game saves.  Remove it manually if you no longer need it:"
    Write-Warn "  $isolatedSave"
}

# Docs and templates (optional extras).
foreach ($extra in @("docs", "templates"))
{
    $extraPath = Join-Path $GamePath $extra
    if (Test-Path -LiteralPath $extraPath -PathType Container)
    {
        Remove-IfPresent -Path $extraPath -Label "$extra/"
    }
}

# Install/uninstall scripts copied into game dir.
foreach ($scriptName in @("Install-Crabtya.ps1", "Uninstall-Crabtya.ps1"))
{
    $scriptPath = Join-Path $GamePath $scriptName
    if (Test-Path -LiteralPath $scriptPath -PathType Leaf)
    {
        Remove-IfPresent -Path $scriptPath -Label $scriptName
    }
}

# ---------------------------------------------------------------------------
# Optionally remove Mods/
# ---------------------------------------------------------------------------

$modsDir = Join-Path $GamePath "Mods"
if ($RemoveMods)
{
    Write-Step "Removing Mods/ directory..."
    Remove-IfPresent -Path $modsDir -Label "Mods/"
}
else
{
    if (Test-Path -LiteralPath $modsDir)
    {
        Write-Skip "Mods/ preserved.  Re-run with -RemoveMods to delete mod data."
    }
}

# ---------------------------------------------------------------------------
# Post-uninstall verification
# ---------------------------------------------------------------------------

Write-Host ""
Write-Host "Uninstall complete." -ForegroundColor Green
Write-Host ""

$loaderPlugin = Join-Path $GamePath "BepInEx\plugins\EIC.ModLoader.dll"
if (-not (Test-Path -LiteralPath $loaderPlugin))
{
    Write-Ok "Loader plugin removed."
}
else
{
    Write-Host "  [WARN] Loader plugin still present: $loaderPlugin" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Launch 'Everything is Crab.exe' — it should start without the Crabtya overlay."
Write-Host "  2. If it still shows Crabtya, check that winhttp.dll was removed from the game folder."
Write-Host "  3. Base game saves are untouched; modded-session saves remain in CrabtyaData/ if present."
Write-Host ""
