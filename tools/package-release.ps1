[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$GameRoot,
    [string]$OutputRoot,
    [string]$ReleaseLabel = "v1-candidate",
    [string[]]$ModId = @(),
    [switch]$SkipLoader,
    [switch]$SkipLite,
    [switch]$SkipMods,
    [switch]$SkipTemplates,
    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $PSCommandPath
if ([string]::IsNullOrWhiteSpace($scriptRoot))
{
    $scriptRoot = (Get-Location).Path
}

if ([string]::IsNullOrWhiteSpace($RepoRoot))
{
    $RepoRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path
}

if ([string]::IsNullOrWhiteSpace($GameRoot))
{
    $GameRoot = Join-Path $RepoRoot "game"
}

if ([string]::IsNullOrWhiteSpace($OutputRoot))
{
    $OutputRoot = Join-Path $RepoRoot "packages"
}

$modsRoot = Join-Path $GameRoot "Mods"
$loaderPluginPath = Join-Path $GameRoot "BepInEx\plugins\EIC.ModLoader.dll"
$litePluginPath = Join-Path $GameRoot "BepInEx\plugins\Crabtya.Lite.dll"
$loaderApiPath = Join-Path $GameRoot "BepInEx\plugins\Crabtya.ModApi.dll"
$modIdPattern = "^[A-Za-z0-9]+(?:[._-][A-Za-z0-9]+)+$"
$appliedDefinitionTypes = @("localization", "balancePatch", "cameraPatch", "visual", "introSkip", "uiScale")
$discoveredOnlyDefinitionTypes = @("evolution", "enemy")

function Assert-Exists {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path))
    {
        throw "$Label not found: $Path"
    }
}

function Remove-DirectoryIfPresent {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (Test-Path -LiteralPath $Path)
    {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function New-EmptyDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    Remove-DirectoryIfPresent -Path $Path
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function New-DirectoryIfMissing {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path))
    {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Copy-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    Assert-Exists -Path $Source -Label "Directory"
    New-DirectoryIfMissing -Path $Destination

    foreach ($item in Get-ChildItem -LiteralPath $Source -Force)
    {
        Copy-Item -LiteralPath $item.FullName -Destination (Join-Path $Destination $item.Name) -Recurse -Force
    }
}

function Copy-FileWithParents {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    Assert-Exists -Path $Source -Label "File"
    $parent = Split-Path -Parent $Destination
    if (-not [string]::IsNullOrWhiteSpace($parent))
    {
        New-DirectoryIfMissing -Path $parent
    }

    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}

function Convert-ToRelativeOsPath {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    return $RelativePath.Replace("/", [System.IO.Path]::DirectorySeparatorChar)
}

function Get-ManifestInfo {
    param([Parameter(Mandatory = $true)][string]$ModDirectory)

    $folderName = Split-Path -Leaf $ModDirectory
    $manifestPath = Join-Path $ModDirectory "eicmod.json"
    $errors = New-Object System.Collections.Generic.List[string]
    $warnings = New-Object System.Collections.Generic.List[string]
    $manifest = $null

    if (-not (Test-Path -LiteralPath $manifestPath))
    {
        $errors.Add("Missing eicmod.json.")
    }
    else
    {
        try
        {
            $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        }
        catch
        {
            $errors.Add("Manifest parse error: $($_.Exception.Message)")
        }
    }

    [pscustomobject]@{
        FolderName = $folderName
        DirectoryPath = $ModDirectory
        ManifestPath = $manifestPath
        Manifest = $manifest
        Errors = $errors
        Warnings = $warnings
    }
}

function Validate-RelativeFileList {
    param(
        [Parameter(Mandatory = $true)]$Info,
        [Parameter(Mandatory = $true)][string]$Kind,
        [Parameter(Mandatory = $false)]$Paths
    )

    foreach ($relativePath in @($Paths))
    {
        if ([string]::IsNullOrWhiteSpace($relativePath))
        {
            $Info.Errors.Add("$Kind path is empty.")
            continue
        }

        if ([System.IO.Path]::IsPathRooted($relativePath))
        {
            $Info.Errors.Add("$Kind path must be relative: $relativePath")
            continue
        }

        if ($relativePath.Contains(".."))
        {
            $Info.Errors.Add("$Kind path cannot traverse parent directories: $relativePath")
            continue
        }

        $resolved = Join-Path $Info.DirectoryPath (Convert-ToRelativeOsPath -RelativePath $relativePath)
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf))
        {
            $Info.Errors.Add("Referenced $Kind file is missing: $relativePath")
        }
    }
}

function Discover-DefinitionWarnings {
    param([Parameter(Mandatory = $true)]$Info)

    $definitions = @()
    $manifestContent = $null
    if ($null -ne $Info.Manifest -and $null -ne $Info.Manifest.PSObject.Properties["content"])
    {
        $manifestContent = $Info.Manifest.content
    }
    if ($null -ne $manifestContent -and $null -ne $manifestContent.PSObject.Properties["definitions"])
    {
        $definitions = @($manifestContent.definitions)
    }

    foreach ($relativePath in $definitions)
    {
        $resolved = Join-Path $Info.DirectoryPath (Convert-ToRelativeOsPath -RelativePath $relativePath)
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf))
        {
            continue
        }

        try
        {
            $definition = Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
            $definitionType = Get-ManifestString $definition "type"
            if ([string]::IsNullOrWhiteSpace($definitionType))
            {
                $Info.Warnings.Add("Definition file '$relativePath' is missing string field 'type'.")
                continue
            }

            if ($appliedDefinitionTypes -icontains $definitionType)
            {
                continue
            }

            if ($discoveredOnlyDefinitionTypes -icontains $definitionType)
            {
                $Info.Warnings.Add("Definition file '$relativePath' uses recognized future type '$definitionType'. This type is packaged for planning/prototyping only and has no runtime applicator yet.")
                continue
            }

            $Info.Warnings.Add("Definition file '$relativePath' uses unsupported type '$definitionType'.")
        }
        catch
        {
            $Info.Warnings.Add("Definition file '$relativePath' parse error: $($_.Exception.Message)")
        }
    }
}

function Get-ManifestString {
    param($Manifest, [string]$Field)
    if ($null -ne $Manifest -and $null -ne $Manifest.PSObject.Properties[$Field])
    {
        return [string]$Manifest.$Field
    }
    return ""
}

function Validate-ModInfo {
    param(
        [Parameter(Mandatory = $true)]$Info,
        [Parameter(Mandatory = $true)][hashtable]$IdCounts,
        [Parameter(Mandatory = $true)][System.Collections.Generic.HashSet[string]]$KnownIds
    )

    $manifest = $Info.Manifest
    if ($null -eq $manifest)
    {
        if ($Info.Errors.Count -eq 0)
        {
            $Info.Errors.Add("Manifest is null after parse.")
        }

        return
    }

    $manifestId      = Get-ManifestString $manifest "id"
    $manifestName    = Get-ManifestString $manifest "name"
    $manifestVersion = Get-ManifestString $manifest "version"
    $manifestAuthor  = Get-ManifestString $manifest "author"
    $manifestDesc    = Get-ManifestString $manifest "description"
    $loaderVersion   = Get-ManifestString $manifest "loaderVersion"
    $targetGame      = Get-ManifestString $manifest "targetGame"
    $targetUnity     = Get-ManifestString $manifest "targetUnity"
    $manifestContent = if ($null -ne $manifest.PSObject.Properties["content"]) { $manifest.content } else { $null }

    if ([string]::IsNullOrWhiteSpace($manifestId))
    {
        $Info.Errors.Add("Missing required field 'id'.")
    }

    if ([string]::IsNullOrWhiteSpace($manifestName))
    {
        $Info.Errors.Add("Missing required field 'name'.")
    }

    if ([string]::IsNullOrWhiteSpace($manifestVersion))
    {
        $Info.Errors.Add("Missing required field 'version'.")
    }

    if ([string]::IsNullOrWhiteSpace($manifestAuthor))
    {
        $Info.Errors.Add("Missing required field 'author'.")
    }

    if ([string]::IsNullOrWhiteSpace($manifestDesc))
    {
        $Info.Errors.Add("Missing required field 'description'.")
    }

    if ([string]::IsNullOrWhiteSpace($loaderVersion))
    {
        $Info.Errors.Add("Missing required field 'loaderVersion'.")
    }
    elseif ((-not $loaderVersion.StartsWith("1.")) -and (-not [string]::Equals($loaderVersion, "1.x", [System.StringComparison]::OrdinalIgnoreCase)))
    {
        $Info.Warnings.Add("loaderVersion '$loaderVersion' is outside expected major '1.x'.")
    }

    if ($null -eq $manifestContent)
    {
        $Info.Errors.Add("Missing required field 'content'.")
    }

    if (-not [string]::IsNullOrWhiteSpace($manifestId) -and ($manifestId -notmatch $modIdPattern))
    {
        $Info.Errors.Add("Field 'id' must be reverse-domain style (for example: author.mod-id).")
    }

    if (-not [string]::Equals($targetGame, "Everything is Crab", [System.StringComparison]::OrdinalIgnoreCase))
    {
        $Info.Errors.Add("targetGame must be 'Everything is Crab'.")
    }

    if (-not [string]::IsNullOrWhiteSpace($targetUnity) -and -not [string]::Equals($targetUnity, "6000.2.15f1", [System.StringComparison]::OrdinalIgnoreCase))
    {
        $Info.Warnings.Add("targetUnity '$targetUnity' differs from expected '6000.2.15f1'.")
    }

    if (-not [string]::IsNullOrWhiteSpace($manifestId) -and $IdCounts.ContainsKey($manifestId) -and $IdCounts[$manifestId] -gt 1)
    {
        $Info.Errors.Add("Duplicate mod id detected.")
    }

    $dependencies = @()
    if ($null -ne $manifest.PSObject.Properties["dependencies"])
    {
        $dependencies = @($manifest.dependencies)
    }
    foreach ($dependency in $dependencies)
    {
        if ([string]::IsNullOrWhiteSpace([string]$dependency))
        {
            $Info.Errors.Add("Dependency id is empty.")
            continue
        }

        if (-not $KnownIds.Contains([string]$dependency))
        {
            $Info.Errors.Add("Missing dependency '$dependency'.")
        }
    }

    if (-not [string]::Equals($Info.FolderName, $manifestId, [System.StringComparison]::OrdinalIgnoreCase))
    {
        $Info.Warnings.Add("Folder name '$($Info.FolderName)' differs from manifest id '$manifestId'. The package will be rooted at '$manifestId'.")
    }

    $catalogs = @()
    $assetBundles = @()
    $definitions = @()
    if ($null -ne $manifestContent)
    {
        if ($null -ne $manifestContent.PSObject.Properties["catalogs"])
        {
            $catalogs = @($manifestContent.catalogs)
        }
        if ($null -ne $manifestContent.PSObject.Properties["assetBundles"])
        {
            $assetBundles = @($manifestContent.assetBundles)
        }
        if ($null -ne $manifestContent.PSObject.Properties["definitions"])
        {
            $definitions = @($manifestContent.definitions)
        }
    }

    $assemblies = @()
    if ($null -ne $manifest.PSObject.Properties["assemblies"])
    {
        $assemblies = @($manifest.assemblies)
    }

    Validate-RelativeFileList -Info $Info -Kind "catalog" -Paths $catalogs
    Validate-RelativeFileList -Info $Info -Kind "assetBundle" -Paths $assetBundles
    Validate-RelativeFileList -Info $Info -Kind "definition" -Paths $definitions
    Validate-RelativeFileList -Info $Info -Kind "assembly" -Paths $assemblies

    $entrypoints = @()
    if ($null -ne $manifest.PSObject.Properties["entrypoints"])
    {
        $entrypoints = @($manifest.entrypoints)
    }
    foreach ($entrypoint in $entrypoints)
    {
        if ([string]::IsNullOrWhiteSpace([string]$entrypoint))
        {
            $Info.Errors.Add("Entrypoint type name is empty.")
        }
    }

    $hasAssemblies = $assemblies.Count -gt 0
    $hasEntrypoints = $entrypoints.Count -gt 0
    if ($hasAssemblies -ne $hasEntrypoints)
    {
        $Info.Errors.Add("DLL mods must declare both 'assemblies' and 'entrypoints'.")
    }

    Discover-DefinitionWarnings -Info $Info
}

function New-ZipFromDirectoryContents {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDirectory,
        [Parameter(Mandatory = $true)][string]$ZipPath
    )

    $items = @(Get-ChildItem -LiteralPath $SourceDirectory -Force | Sort-Object FullName)
    if ($items.Count -eq 0)
    {
        throw "No files staged for zip: $SourceDirectory"
    }

    if (Test-Path -LiteralPath $ZipPath)
    {
        Remove-Item -LiteralPath $ZipPath -Force
    }

    Compress-Archive -LiteralPath $items.FullName -DestinationPath $ZipPath -CompressionLevel Optimal
}

function New-LoaderPackage {
    param(
        [Parameter(Mandatory = $true)][string]$StageRoot,
        [Parameter(Mandatory = $true)][string]$ZipPath
    )

    $pluginFile = Get-Item -LiteralPath $loaderPluginPath
    New-EmptyDirectory -Path $StageRoot

    foreach ($rootFile in @(".doorstop_version", "changelog.txt", "doorstop_config.ini", "winhttp.dll"))
    {
        Copy-FileWithParents -Source (Join-Path $GameRoot $rootFile) -Destination (Join-Path $StageRoot $rootFile)
    }

    Copy-DirectoryContents -Source (Join-Path $GameRoot "BepInEx\core") -Destination (Join-Path $StageRoot "BepInEx\core")
    Copy-DirectoryContents -Source (Join-Path $GameRoot "BepInEx\interop") -Destination (Join-Path $StageRoot "BepInEx\interop")
    Copy-DirectoryContents -Source (Join-Path $GameRoot "BepInEx\unity-libs") -Destination (Join-Path $StageRoot "BepInEx\unity-libs")
    Copy-DirectoryContents -Source (Join-Path $GameRoot "dotnet") -Destination (Join-Path $StageRoot "dotnet")
    Copy-FileWithParents -Source (Join-Path $GameRoot "BepInEx\config\BepInEx.cfg") -Destination (Join-Path $StageRoot "BepInEx\config\BepInEx.cfg")
    Copy-FileWithParents -Source $loaderPluginPath -Destination (Join-Path $StageRoot "BepInEx\plugins\EIC.ModLoader.dll")
    Copy-FileWithParents -Source $loaderApiPath -Destination (Join-Path $StageRoot "BepInEx\plugins\Crabtya.ModApi.dll")

    Copy-FileWithParents -Source (Join-Path $RepoRoot "docs\README.md") -Destination (Join-Path $StageRoot "docs\README.md")
    Copy-FileWithParents -Source (Join-Path $RepoRoot "docs\users\install-uninstall.md") -Destination (Join-Path $StageRoot "docs\users\install-uninstall.md")
    Copy-FileWithParents -Source (Join-Path $RepoRoot "docs\users\safe-mode.md") -Destination (Join-Path $StageRoot "docs\users\safe-mode.md")
    Copy-FileWithParents -Source (Join-Path $RepoRoot "docs\users\troubleshooting.md") -Destination (Join-Path $StageRoot "docs\users\troubleshooting.md")

    # Install / uninstall scripts.
    Copy-FileWithParents -Source (Join-Path $scriptRoot "Install-Crabtya.ps1") -Destination (Join-Path $StageRoot "Install-Crabtya.ps1")
    Copy-FileWithParents -Source (Join-Path $scriptRoot "Uninstall-Crabtya.ps1") -Destination (Join-Path $StageRoot "Uninstall-Crabtya.ps1")

    # Mod templates (optional: present only when the templates directory exists).
    $templatesSource = Join-Path $RepoRoot "templates"
    if (Test-Path -LiteralPath $templatesSource -PathType Container)
    {
        Copy-DirectoryContents -Source $templatesSource -Destination (Join-Path $StageRoot "templates")
    }

    $readmePath = Join-Path $StageRoot "README.txt"
    $readme = @(
        "Crabtya Loader Package"
        "Release label: $ReleaseLabel"
        ""
        "Source plugin fingerprint:"
        "- File: BepInEx\\plugins\\EIC.ModLoader.dll"
        "- Size: $($pluginFile.Length) bytes"
        "- LastWriteUtc: $($pluginFile.LastWriteTimeUtc.ToString('o'))"
        "- API: BepInEx\\plugins\\Crabtya.ModApi.dll"
        ""
        "Quick Install (PowerShell):"
        "1. Close the game."
        "2. Extract this zip to a temporary folder."
        "3. Open PowerShell in that folder and run:"
        "     .\\Install-Crabtya.ps1 -GamePath `"C:\\path\\to\\Everything is Crab`""
        "4. Launch the game and confirm BepInEx\\LogOutput.log contains 'Plugin binary fingerprint'."
        "5. Confirm the main menu shows 'Crabtya loaded v<version>'."
        ""
        "Manual Install:"
        "1. Close the game."
        "2. Extract this zip directly into the folder beside 'Everything is Crab.exe'."
        "3. The BepInEx/, dotnet/, and doorstop files must land beside the game exe."
        ""
        "Uninstall:"
        "     .\\Uninstall-Crabtya.ps1 -GamePath `"C:\\path\\to\\Everything is Crab`""
        "Or manually remove: BepInEx/, dotnet/, winhttp.dll, doorstop_config.ini, .doorstop_version, changelog.txt"
        ""
        "Mods:"
        "- Extract each mod zip into Mods\\ so the manifest lands at Mods\\<mod-id>\\eicmod.json."
        ""
        "Docs shipped in this package:"
        "- docs\\users\\install-uninstall.md"
        "- docs\\users\\safe-mode.md"
        "- docs\\users\\troubleshooting.md"
        ""
        "Mod templates (starter kits for mod makers):"
        "- templates\\content-mod-template\\"
        "- templates\\dll-mod-template\\"
    ) -join [Environment]::NewLine
    Set-Content -LiteralPath $readmePath -Value $readme -Encoding ASCII

    New-ZipFromDirectoryContents -SourceDirectory $StageRoot -ZipPath $ZipPath

    [pscustomobject]@{
        ZipPath = $ZipPath
        ReleaseLabel = $ReleaseLabel
        PluginSize = $pluginFile.Length
        PluginLastWriteUtc = $pluginFile.LastWriteTimeUtc.ToString("o")
    }
}

function New-LitePackage {
    param(
        [Parameter(Mandatory = $true)][string]$StageRoot,
        [Parameter(Mandatory = $true)][string]$ZipPath
    )

    $pluginFile = Get-Item -LiteralPath $litePluginPath
    New-EmptyDirectory -Path $StageRoot

    foreach ($rootFile in @(".doorstop_version", "changelog.txt", "doorstop_config.ini", "winhttp.dll"))
    {
        Copy-FileWithParents -Source (Join-Path $GameRoot $rootFile) -Destination (Join-Path $StageRoot $rootFile)
    }

    Copy-DirectoryContents -Source (Join-Path $GameRoot "BepInEx\core") -Destination (Join-Path $StageRoot "BepInEx\core")
    Copy-DirectoryContents -Source (Join-Path $GameRoot "BepInEx\interop") -Destination (Join-Path $StageRoot "BepInEx\interop")
    Copy-DirectoryContents -Source (Join-Path $GameRoot "BepInEx\unity-libs") -Destination (Join-Path $StageRoot "BepInEx\unity-libs")
    Copy-DirectoryContents -Source (Join-Path $GameRoot "dotnet") -Destination (Join-Path $StageRoot "dotnet")
    Copy-FileWithParents -Source (Join-Path $GameRoot "BepInEx\config\BepInEx.cfg") -Destination (Join-Path $StageRoot "BepInEx\config\BepInEx.cfg")
    Copy-FileWithParents -Source $litePluginPath -Destination (Join-Path $StageRoot "BepInEx\plugins\Crabtya.Lite.dll")

    Copy-FileWithParents -Source (Join-Path $RepoRoot "docs\README.md") -Destination (Join-Path $StageRoot "docs\README.md")
    Copy-FileWithParents -Source (Join-Path $RepoRoot "docs\users\crabtya-lite.md") -Destination (Join-Path $StageRoot "docs\users\crabtya-lite.md")
    Copy-FileWithParents -Source (Join-Path $RepoRoot "docs\users\troubleshooting.md") -Destination (Join-Path $StageRoot "docs\users\troubleshooting.md")

    # Install / uninstall scripts.
    Copy-FileWithParents -Source (Join-Path $scriptRoot "Install-CrabtyaLite.ps1") -Destination (Join-Path $StageRoot "Install-CrabtyaLite.ps1")
    Copy-FileWithParents -Source (Join-Path $scriptRoot "Uninstall-CrabtyaLite.ps1") -Destination (Join-Path $StageRoot "Uninstall-CrabtyaLite.ps1")

    $readmePath = Join-Path $StageRoot "README.txt"
    $readme = @(
        "Crabtya Lite Package"
        "Release label: $ReleaseLabel"
        ""
        "Source plugin fingerprint:"
        "- File: BepInEx\\plugins\\Crabtya.Lite.dll"
        "- Size: $($pluginFile.Length) bytes"
        "- LastWriteUtc: $($pluginFile.LastWriteTimeUtc.ToString('o'))"
        ""
        "Crabtya Lite is a curated QoL-only plugin (zoom/invert/FOV)."
        "It is not a folder-based mod loader and does not load game/Mods manifests."
        ""
        "Quick Install (PowerShell):"
        "1. Close the game."
        "2. Extract this zip to a temporary folder."
        "3. Open PowerShell in that folder and run:"
        "     .\\Install-CrabtyaLite.ps1 -GamePath `"C:\\path\\to\\Everything is Crab`""
        "4. Launch the game and confirm BepInEx\\LogOutput.log contains 'Crabtya Lite runtime initialized'."
        ""
        "Compatibility:"
        "- Do not install full Crabtya and Crabtya Lite together unless you intentionally override guards for local testing."
        ""
        "Uninstall:"
        "     .\\Uninstall-CrabtyaLite.ps1 -GamePath `"C:\\path\\to\\Everything is Crab`""
        ""
        "Docs shipped in this package:"
        "- docs\\users\\crabtya-lite.md"
        "- docs\\users\\troubleshooting.md"
    ) -join [Environment]::NewLine
    Set-Content -LiteralPath $readmePath -Value $readme -Encoding ASCII

    New-ZipFromDirectoryContents -SourceDirectory $StageRoot -ZipPath $ZipPath

    [pscustomobject]@{
        ZipPath = $ZipPath
        ReleaseLabel = $ReleaseLabel
        PluginSize = $pluginFile.Length
        PluginLastWriteUtc = $pluginFile.LastWriteTimeUtc.ToString("o")
    }
}

function New-ModPackage {
    param(
        [Parameter(Mandatory = $true)]$Info,
        [Parameter(Mandatory = $true)][string]$StageRoot,
        [Parameter(Mandatory = $true)][string]$ZipPath
    )

    $packageRoot = Join-Path $StageRoot (Get-ManifestString $Info.Manifest "id")
    New-EmptyDirectory -Path $StageRoot
    New-DirectoryIfMissing -Path $packageRoot

    foreach ($file in Get-ChildItem -LiteralPath $Info.DirectoryPath -File -Recurse | Sort-Object FullName)
    {
        $relativePath = $file.FullName.Substring($Info.DirectoryPath.Length).TrimStart("\\")
        $destination = Join-Path $packageRoot $relativePath
        Copy-FileWithParents -Source $file.FullName -Destination $destination
    }

    New-ZipFromDirectoryContents -SourceDirectory $StageRoot -ZipPath $ZipPath

    [pscustomobject]@{
        Id = Get-ManifestString $Info.Manifest "id"
        Name = Get-ManifestString $Info.Manifest "name"
        Version = Get-ManifestString $Info.Manifest "version"
        ZipPath = $ZipPath
        WarningCount = $Info.Warnings.Count
        Warnings = @($Info.Warnings)
    }
}

function New-TemplatePackage {
    param(
        [Parameter(Mandatory = $true)][string]$TemplateDirectory,
        [Parameter(Mandatory = $true)][string]$StageRoot,
        [Parameter(Mandatory = $true)][string]$ZipPath
    )

    $templateName = Split-Path -Leaf $TemplateDirectory
    $packageRoot = Join-Path $StageRoot $templateName
    New-EmptyDirectory -Path $StageRoot
    New-DirectoryIfMissing -Path $packageRoot

    foreach ($file in Get-ChildItem -LiteralPath $TemplateDirectory -File -Recurse | Sort-Object FullName)
    {
        $relativePath = $file.FullName.Substring($TemplateDirectory.Length).TrimStart("\\/")
        $destination = Join-Path $packageRoot $relativePath
        Copy-FileWithParents -Source $file.FullName -Destination $destination
    }

    New-ZipFromDirectoryContents -SourceDirectory $StageRoot -ZipPath $ZipPath

    [pscustomobject]@{
        Name = $templateName
        ZipPath = $ZipPath
    }
}

Assert-Exists -Path $RepoRoot -Label "Repo root"
Assert-Exists -Path $GameRoot -Label "Game root"
Assert-Exists -Path $modsRoot -Label "Mods root"

$loaderOutputRoot = Join-Path $OutputRoot "loader"
$liteOutputRoot = Join-Path $OutputRoot "lite"
$modsOutputRoot = Join-Path $OutputRoot "mods"
$templatesOutputRoot = Join-Path $OutputRoot "templates"
$stagingRoot = Join-Path $OutputRoot "_staging"

if ($Clean)
{
    Remove-DirectoryIfPresent -Path $loaderOutputRoot
    Remove-DirectoryIfPresent -Path $liteOutputRoot
    Remove-DirectoryIfPresent -Path $modsOutputRoot
    Remove-DirectoryIfPresent -Path $templatesOutputRoot
    Remove-DirectoryIfPresent -Path $stagingRoot
}

New-DirectoryIfMissing -Path $OutputRoot
New-DirectoryIfMissing -Path $loaderOutputRoot
New-DirectoryIfMissing -Path $liteOutputRoot
New-DirectoryIfMissing -Path $modsOutputRoot
New-DirectoryIfMissing -Path $templatesOutputRoot
New-DirectoryIfMissing -Path $stagingRoot

$allModInfos = @()
foreach ($directory in Get-ChildItem -LiteralPath $modsRoot -Directory | Sort-Object Name)
{
    if ([string]::Equals($directory.Name, "_disabled", [System.StringComparison]::OrdinalIgnoreCase))
    {
        continue
    }

    if (-not (Test-Path -LiteralPath (Join-Path $directory.FullName "eicmod.json")))
    {
        continue
    }

    $allModInfos += Get-ManifestInfo -ModDirectory $directory.FullName
}

$idCounts = @{}
$knownIds = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
foreach ($info in $allModInfos)
{
    $manifestId = Get-ManifestString $info.Manifest "id"
    if (-not [string]::IsNullOrWhiteSpace($manifestId))
    {
        if ($idCounts.ContainsKey($manifestId))
        {
            $idCounts[$manifestId]++
        }
        else
        {
            $idCounts[$manifestId] = 1
        }

        $knownIds.Add($manifestId) | Out-Null
    }
}

foreach ($info in $allModInfos)
{
    Validate-ModInfo -Info $info -IdCounts $idCounts -KnownIds $knownIds
}

$selectedModInfos = $allModInfos
if ($ModId.Count -gt 0)
{
    $requested = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($requestedId in $ModId)
    {
        if (-not [string]::IsNullOrWhiteSpace($requestedId))
        {
            $requested.Add($requestedId) | Out-Null
        }
    }

    $selectedModInfos = @(
        $allModInfos | Where-Object {
            $requested.Contains($_.FolderName) -or $requested.Contains((Get-ManifestString $_.Manifest "id"))
        }
    )

    $selectedNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($info in $selectedModInfos)
    {
        $selectedNames.Add($info.FolderName) | Out-Null
        $infoId = Get-ManifestString $info.Manifest "id"
        if (-not [string]::IsNullOrWhiteSpace($infoId))
        {
            $selectedNames.Add($infoId) | Out-Null
        }
    }

    $missingRequested = @($requested | Where-Object { -not $selectedNames.Contains($_) })
    if ($missingRequested.Count -gt 0)
    {
        throw "Requested mod id(s) not found: $($missingRequested -join ', ')"
    }
}

$selectedValidationErrors = @(
    foreach ($info in $selectedModInfos)
    {
        foreach ($error in $info.Errors)
        {
            "[$($info.FolderName)] $error"
        }
    }
)

if (-not $SkipMods -and $selectedValidationErrors.Count -gt 0)
{
    throw "Packaging validation failed:`n$($selectedValidationErrors -join [Environment]::NewLine)"
}

$loaderSummary = $null
if (-not $SkipLoader)
{
    Assert-Exists -Path $loaderPluginPath -Label "Loader plugin"
    Assert-Exists -Path $loaderApiPath -Label "Crabtya Mod API plugin"
    $loaderStageRoot = Join-Path $stagingRoot "loader"
    $loaderZipPath = Join-Path $loaderOutputRoot "EverythingIsCrab.ModLoader-$ReleaseLabel.zip"
    $loaderSummary = New-LoaderPackage -StageRoot $loaderStageRoot -ZipPath $loaderZipPath
}

$liteSummary = $null
if (-not $SkipLite)
{
    if (Test-Path -LiteralPath $litePluginPath -PathType Leaf)
    {
        $liteStageRoot = Join-Path $stagingRoot "lite"
        $liteZipPath = Join-Path $liteOutputRoot "Crabtya-Lite-$ReleaseLabel.zip"
        $liteSummary = New-LitePackage -StageRoot $liteStageRoot -ZipPath $liteZipPath
    }
    else
    {
        Write-Warning "Crabtya Lite plugin not found: $litePluginPath. Skipping Lite package."
    }
}

$modSummaries = @()
if (-not $SkipMods)
{
    foreach ($info in $selectedModInfos | Sort-Object { Get-ManifestString $_.Manifest "id" })
    {
        $infoId      = Get-ManifestString $info.Manifest "id"
        $infoVersion = Get-ManifestString $info.Manifest "version"
        $modStageRoot = Join-Path $stagingRoot $infoId
        $modZipPath = Join-Path $modsOutputRoot ($infoId + "-" + $infoVersion + ".zip")
        $modSummaries += New-ModPackage -Info $info -StageRoot $modStageRoot -ZipPath $modZipPath
    }
}

$templateSummaries = @()
if (-not $SkipTemplates)
{
    $templatesSourceRoot = Join-Path $RepoRoot "templates"
    if (Test-Path -LiteralPath $templatesSourceRoot -PathType Container)
    {
        foreach ($templateDir in Get-ChildItem -LiteralPath $templatesSourceRoot -Directory | Sort-Object Name)
        {
            $templateStageRoot = Join-Path $stagingRoot $templateDir.Name
            $templateZipPath = Join-Path $templatesOutputRoot ($templateDir.Name + "-" + $ReleaseLabel + ".zip")
            $templateSummaries += New-TemplatePackage -TemplateDirectory $templateDir.FullName -StageRoot $templateStageRoot -ZipPath $templateZipPath
        }
    }
}

$summaryPath = Join-Path $OutputRoot "package-summary.json"
$summary = [ordered]@{
    createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    releaseLabel = $ReleaseLabel
    outputRoot = $OutputRoot
    loaderPackage = $loaderSummary
    litePackage = $liteSummary
    modPackages = $modSummaries
    templatePackages = $templateSummaries
}

$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $summaryPath -Encoding ASCII
Remove-DirectoryIfPresent -Path $stagingRoot

$summary | ConvertTo-Json -Depth 6
