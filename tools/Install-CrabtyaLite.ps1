[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)]
    [string]$GamePath,

    [Parameter(Mandatory = $false)]
    [string]$PackageZip = "",

    [switch]$AllowFullCoexist
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

function Write-Fail {
    param([string]$Message)
    Write-Host "  [FAIL] $Message" -ForegroundColor Red
}

$scriptDir = Split-Path -Parent $PSCommandPath
if ([string]::IsNullOrWhiteSpace($scriptDir))
{
    $scriptDir = (Get-Location).Path
}

if ([string]::IsNullOrWhiteSpace($PackageZip))
{
    $candidates = @(Get-ChildItem -LiteralPath $scriptDir -Filter "Crabtya-Lite-*.zip" -File 2>$null)
    if ($candidates.Count -eq 0)
    {
        $candidates = @(Get-ChildItem -LiteralPath (Split-Path -Parent $scriptDir) -Filter "Crabtya-Lite-*.zip" -Recurse -File 2>$null)
    }

    if ($candidates.Count -eq 0)
    {
        Write-Fail "No Crabtya Lite zip found. Pass -PackageZip to specify the zip path explicitly."
        exit 1
    }

    $PackageZip = ($candidates | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
    Write-Warn "No -PackageZip specified; using: $PackageZip"
}

if (-not (Test-Path -LiteralPath $PackageZip -PathType Leaf))
{
    Write-Fail "Package zip not found: $PackageZip"
    exit 1
}

$GamePath = [System.IO.Path]::GetFullPath($GamePath)
if (-not (Test-Path -LiteralPath $GamePath -PathType Container))
{
    Write-Fail "Game path does not exist: $GamePath"
    exit 1
}

$gameExe = Join-Path $GamePath "Everything is Crab.exe"
if (-not (Test-Path -LiteralPath $gameExe -PathType Leaf))
{
    Write-Warn "Did not find 'Everything is Crab.exe' in: $GamePath"
    Write-Warn "Proceeding anyway — ensure the path is correct before launching the game."
}

$fullPlugin = Join-Path $GamePath "BepInEx\plugins\EIC.ModLoader.dll"
if ((Test-Path -LiteralPath $fullPlugin -PathType Leaf) -and -not $AllowFullCoexist)
{
    Write-Fail "Detected full Crabtya plugin at: $fullPlugin"
    Write-Fail "Crabtya Lite install is blocked by default when full Crabtya is present."
    Write-Fail "Remove full Crabtya first, or rerun with -AllowFullCoexist for local testing only."
    exit 1
}

Write-Host ""
Write-Host "Crabtya Lite Installer" -ForegroundColor White
Write-Host "  Game path  : $GamePath"
Write-Host "  Package zip: $PackageZip"
Write-Host ""

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("crabtya-lite-install-" + [System.Guid]::NewGuid().ToString("N"))

try
{
    Write-Step "Extracting Crabtya Lite package..."
    Expand-Archive -LiteralPath $PackageZip -DestinationPath $tempDir -Force

    $extractedItems = @(Get-ChildItem -LiteralPath $tempDir -Force)
    $sourceRoot = $tempDir
    if ($extractedItems.Count -eq 1 -and $extractedItems[0].PSIsContainer)
    {
        $sourceRoot = $extractedItems[0].FullName
    }

    $loaderItems = @(
        "BepInEx",
        "dotnet",
        "doorstop_config.ini",
        "winhttp.dll",
        ".doorstop_version",
        "changelog.txt"
    )

    Write-Step "Copying Lite runtime files into: $GamePath"
    $copied = 0
    foreach ($item in $loaderItems)
    {
        $src = Join-Path $sourceRoot $item
        if (-not (Test-Path -LiteralPath $src))
        {
            Write-Warn "Expected item not found in package: $item (skipping)"
            continue
        }

        $dst = Join-Path $GamePath $item
        if ($PSCmdlet.ShouldProcess($dst, "Copy from package"))
        {
            Copy-Item -LiteralPath $src -Destination $dst -Recurse -Force
            $copied++
        }
    }

    Write-Ok "Copied $copied Lite runtime items."

    $docsSource = Join-Path $sourceRoot "docs"
    if (Test-Path -LiteralPath $docsSource -PathType Container)
    {
        $docsDest = Join-Path $GamePath "docs"
        if ($PSCmdlet.ShouldProcess($docsDest, "Copy docs"))
        {
            Copy-Item -LiteralPath $docsSource -Destination $docsDest -Recurse -Force
            Write-Ok "Copied bundled docs to: $docsDest"
        }
    }

    foreach ($scriptName in @("Install-CrabtyaLite.ps1", "Uninstall-CrabtyaLite.ps1"))
    {
        $scriptSrc = Join-Path $sourceRoot $scriptName
        if (Test-Path -LiteralPath $scriptSrc -PathType Leaf)
        {
            $scriptDst = Join-Path $GamePath $scriptName
            if ($PSCmdlet.ShouldProcess($scriptDst, "Copy script"))
            {
                Copy-Item -LiteralPath $scriptSrc -Destination $scriptDst -Force
            }
        }
    }
}
finally
{
    if (Test-Path -LiteralPath $tempDir)
    {
        Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "Install complete." -ForegroundColor Green
Write-Host ""
Write-Host "Verification checklist:" -ForegroundColor White

$checks = @(
    @{ Path = (Join-Path $GamePath "BepInEx\plugins\Crabtya.Lite.dll"); Label = "Lite plugin" },
    @{ Path = (Join-Path $GamePath "winhttp.dll"); Label = "Doorstop shim" },
    @{ Path = (Join-Path $GamePath "doorstop_config.ini"); Label = "Doorstop config" }
)

foreach ($check in $checks)
{
    if (Test-Path -LiteralPath $check.Path -PathType Leaf)
    {
        Write-Ok $check.Label
    }
    else
    {
        Write-Fail "$($check.Label) — not found at: $($check.Path)"
    }
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Launch 'Everything is Crab.exe'."
Write-Host "  2. Check BepInEx\LogOutput.log for 'Crabtya Lite runtime initialized'."
Write-Host "  3. Open game settings and confirm Lite sliders for FOV / Invert Scroll / Scroll Zoom."
Write-Host ""
