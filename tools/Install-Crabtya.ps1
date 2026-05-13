<#
.SYNOPSIS
    Installs the Crabtya mod loader into an existing Everything is Crab game directory.

.DESCRIPTION
    Extracts the Crabtya loader package zip (or copies individual files if running from
    an already-extracted package) into the supplied game directory.

    The script:
      - Validates that the target directory looks like an Everything is Crab install.
      - Copies BepInEx/, dotnet/, and the doorstop shim files from the loader package.
      - Does NOT touch the game executable or game data assets.
      - Creates the Mods/ directory if it is absent.
      - On completion prints a verification checklist.

.PARAMETER GamePath
    Full path to the Everything is Crab game directory (the folder that contains
    "Everything is Crab.exe").

.PARAMETER PackageZip
    Path to the Crabtya loader zip file
    (e.g. EverythingIsCrab.ModLoader-v1-candidate.zip).
    If omitted the script looks for a zip in the same directory as itself.

.EXAMPLE
    .\Install-Crabtya.ps1 -GamePath "C:\Games\Everything is Crab"

.EXAMPLE
    .\Install-Crabtya.ps1 -GamePath "C:\Games\Everything is Crab" -PackageZip ".\EverythingIsCrab.ModLoader-v1.zip"
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)]
    [string]$GamePath,

    [Parameter(Mandatory = $false)]
    [string]$PackageZip = ""
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

function Write-Fail {
    param([string]$Message)
    Write-Host "  [FAIL] $Message" -ForegroundColor Red
}

# ---------------------------------------------------------------------------
# Locate package zip
# ---------------------------------------------------------------------------

$scriptDir = Split-Path -Parent $PSCommandPath
if ([string]::IsNullOrWhiteSpace($scriptDir)) { $scriptDir = (Get-Location).Path }

if ([string]::IsNullOrWhiteSpace($PackageZip))
{
    $candidates = @(Get-ChildItem -LiteralPath $scriptDir -Filter "EverythingIsCrab.ModLoader-*.zip" -File 2>$null)
    if ($candidates.Count -eq 0)
    {
        # Also look one level up (packages/loader/ layout)
        $candidates = @(Get-ChildItem -LiteralPath (Split-Path -Parent $scriptDir) -Filter "EverythingIsCrab.ModLoader-*.zip" -Recurse -File 2>$null)
    }

    if ($candidates.Count -eq 0)
    {
        Write-Fail "No loader zip found.  Pass -PackageZip to specify the zip path explicitly."
        exit 1
    }

    # Prefer the most recently modified zip.
    $PackageZip = ($candidates | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
    Write-Warn "No -PackageZip specified; using: $PackageZip"
}

if (-not (Test-Path -LiteralPath $PackageZip -PathType Leaf))
{
    Write-Fail "Package zip not found: $PackageZip"
    exit 1
}

# ---------------------------------------------------------------------------
# Validate game path
# ---------------------------------------------------------------------------

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

# ---------------------------------------------------------------------------
# Extract package into game directory
# ---------------------------------------------------------------------------

Write-Host ""
Write-Host "Crabtya Loader Installer" -ForegroundColor White
Write-Host "  Game path  : $GamePath"
Write-Host "  Package zip: $PackageZip"
Write-Host ""

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("crabtya-install-" + [System.Guid]::NewGuid().ToString("N"))

try
{
    Write-Step "Extracting loader package..."
    Expand-Archive -LiteralPath $PackageZip -DestinationPath $tempDir -Force

    # The zip may have a single root sub-folder (staging artifact) — step into it if so.
    $extractedItems = @(Get-ChildItem -LiteralPath $tempDir -Force)
    $sourceRoot = $tempDir
    if ($extractedItems.Count -eq 1 -and $extractedItems[0].PSIsContainer)
    {
        $sourceRoot = $extractedItems[0].FullName
    }

    # Files and directories that belong to the Crabtya loader (not the game itself).
    $loaderItems = @(
        "BepInEx",
        "dotnet",
        "doorstop_config.ini",
        "winhttp.dll",
        ".doorstop_version",
        "changelog.txt"
    )

    Write-Step "Copying loader files into: $GamePath"
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

    Write-Ok "Copied $copied loader items."

    # Copy bundled docs if present (optional, non-fatal).
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

    # Copy bundled mod templates if present (optional).
    $templatesSource = Join-Path $sourceRoot "templates"
    if (Test-Path -LiteralPath $templatesSource -PathType Container)
    {
        $templatesDest = Join-Path $GamePath "templates"
        if ($PSCmdlet.ShouldProcess($templatesDest, "Copy templates"))
        {
            Copy-Item -LiteralPath $templatesSource -Destination $templatesDest -Recurse -Force
            Write-Ok "Copied mod templates to: $templatesDest"
        }
    }

    # Copy install/uninstall scripts so they are available in the game folder too.
    foreach ($scriptName in @("Install-Crabtya.ps1", "Uninstall-Crabtya.ps1"))
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

    # Ensure the Mods directory exists.
    $modsDir = Join-Path $GamePath "Mods"
    if (-not (Test-Path -LiteralPath $modsDir -PathType Container))
    {
        if ($PSCmdlet.ShouldProcess($modsDir, "Create Mods directory"))
        {
            New-Item -ItemType Directory -Path $modsDir -Force | Out-Null
            Write-Ok "Created Mods/ directory."
        }
    }
    else
    {
        Write-Ok "Mods/ directory already exists."
    }
}
finally
{
    if (Test-Path -LiteralPath $tempDir)
    {
        Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ---------------------------------------------------------------------------
# Post-install checklist
# ---------------------------------------------------------------------------

Write-Host ""
Write-Host "Install complete." -ForegroundColor Green
Write-Host ""
Write-Host "Verification checklist:" -ForegroundColor White

$checks = @(
    @{ Path = (Join-Path $GamePath "BepInEx\plugins\EIC.ModLoader.dll"); Label = "Loader plugin" },
    @{ Path = (Join-Path $GamePath "BepInEx\plugins\Crabtya.ModApi.dll"); Label = "Mod API" },
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
Write-Host "  1. Launch 'Everything is Crab.exe' (or use your existing launcher)."
Write-Host "  2. Check BepInEx\LogOutput.log for 'Plugin binary fingerprint' and 'Crabtya bootstrap loaded'."
Write-Host "  3. Main menu should show a small 'Crabtya loaded v<version>' label and a MODS button."
Write-Host "  4. Drop mod folders into Mods\<mod-id>\ and relaunch to use mods."
Write-Host ""
