[CmdletBinding(SupportsShouldProcess, ConfirmImpact = "High")]
param(
    [Parameter(Mandatory = $true)]
    [string]$GamePath,

    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
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

$GamePath = [System.IO.Path]::GetFullPath($GamePath)
if (-not (Test-Path -LiteralPath $GamePath -PathType Container))
{
    Write-Host "  [FAIL] Game path does not exist: $GamePath" -ForegroundColor Red
    exit 1
}

$gameExe = Join-Path $GamePath "Everything is Crab.exe"
if (-not (Test-Path -LiteralPath $gameExe -PathType Leaf))
{
    Write-Warn "'Everything is Crab.exe' not found in: $GamePath"
    Write-Warn "Proceeding anyway — ensure the game is not running before uninstalling."
}

$fullPlugin = Join-Path $GamePath "BepInEx\plugins\EIC.ModLoader.dll"

Write-Host ""
Write-Host "Crabtya Lite Uninstaller" -ForegroundColor White
Write-Host "  Game path : $GamePath"
if (Test-Path -LiteralPath $fullPlugin -PathType Leaf)
{
    Write-Host "  Full Crabtya plugin detected and will be preserved." -ForegroundColor DarkGray
}
Write-Host ""

if (-not $Force -and -not $PSCmdlet.ShouldContinue(
    "This will remove Crabtya Lite files from the game directory.",
    "Confirm Crabtya Lite Uninstall"))
{
    Write-Host "Uninstall cancelled."
    exit 0
}

Write-Step "Removing Crabtya Lite plugin and settings..."

Remove-IfPresent -Path (Join-Path $GamePath "BepInEx\plugins\Crabtya.Lite.dll") -Label "BepInEx/plugins/Crabtya.Lite.dll"
Remove-IfPresent -Path (Join-Path $GamePath "CrabtyaLite") -Label "CrabtyaLite/ settings"

foreach ($scriptName in @("Install-CrabtyaLite.ps1", "Uninstall-CrabtyaLite.ps1"))
{
    Remove-IfPresent -Path (Join-Path $GamePath $scriptName) -Label $scriptName
}

if (-not (Test-Path -LiteralPath $fullPlugin -PathType Leaf))
{
    Write-Warn "Full Crabtya plugin not detected. Lite uninstall does not remove shared BepInEx runtime files."
    Write-Warn "If you want a full vanilla rollback, run Uninstall-Crabtya.ps1 instead."
}

Write-Host ""
Write-Host "Uninstall complete." -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Launch 'Everything is Crab.exe'."
Write-Host "  2. Confirm BepInEx\LogOutput.log no longer shows 'Loading [Crabtya Lite ...]'."
if (Test-Path -LiteralPath $fullPlugin -PathType Leaf)
{
    Write-Host "  3. Full Crabtya remains installed and should continue to load normally."
}
Write-Host ""
