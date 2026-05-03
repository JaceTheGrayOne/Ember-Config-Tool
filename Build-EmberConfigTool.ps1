param(
    [string]$EmberRoot = "",
    [string]$EmberModFolder = "",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SyncSnapshot,
    [switch]$Test,
    [switch]$Publish
)

$ErrorActionPreference = "Stop"

$RepoRoot = $PSScriptRoot
$Project = Join-Path $RepoRoot "src\Ember_Config_Tool\Ember_Config_Tool.csproj"
$Tests = Join-Path $RepoRoot "tests\Ember_Config_Tool.Tests\Ember_Config_Tool.Tests.csproj"
$SnapshotRoot = Join-Path $RepoRoot "src\Ember_Config_Tool\Assets\ConfigSnapshot"
$ArtifactRoot = Join-Path $RepoRoot "artifacts"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command,
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE."
    }
}

function Resolve-EmberModFolder {
    if ($EmberModFolder) {
        if (-not (Test-Path -LiteralPath $EmberModFolder -PathType Container)) {
            throw "-EmberModFolder does not exist or is not a directory: $EmberModFolder"
        }

        return (Resolve-Path -LiteralPath $EmberModFolder).Path
    }

    if ($EmberRoot) {
        $candidate = Join-Path $EmberRoot "mods\Ember"
        if (Test-Path -LiteralPath $candidate -PathType Container) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }

        throw "-EmberRoot must point to an Ember workspace root containing mods\Ember. Checked: $candidate"
    }

    throw "Snapshot sync requires -EmberRoot <workspace root> or -EmberModFolder <Ember mod folder>."
}

if ($SyncSnapshot) {
    $modFolder = Resolve-EmberModFolder
    $configFolder = Join-Path $modFolder "src\Config"
    $modLua = Join-Path $modFolder "src\mod.lua"
    if (-not (Test-Path -LiteralPath $configFolder -PathType Container) -or
        -not (Test-Path -LiteralPath $modLua -PathType Leaf)) {
        throw "The selected folder is not a valid Ember mod folder: $modFolder"
    }

    $snapshotConfig = Join-Path $SnapshotRoot "Config"
    if (Test-Path -LiteralPath $snapshotConfig) {
        $resolvedSnapshotRoot = (Resolve-Path -LiteralPath $SnapshotRoot).Path
        $resolvedSnapshotConfig = (Resolve-Path -LiteralPath $snapshotConfig).Path
        if (-not $resolvedSnapshotConfig.StartsWith($resolvedSnapshotRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clear snapshot config outside snapshot root: $resolvedSnapshotConfig"
        }

        Remove-Item -LiteralPath $resolvedSnapshotConfig -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $snapshotConfig | Out-Null
    Copy-Item -Force -Path (Join-Path $configFolder "*.lua") -Destination $snapshotConfig
    Copy-Item -Force -Path $modLua -Destination (Join-Path $SnapshotRoot "mod.lua")
    Copy-Item -Force -Path (Join-Path $modFolder "src\User_Config_Overrides_Template.lua") -Destination (Join-Path $SnapshotRoot "User_Config_Overrides_Template.lua")
    Copy-Item -Force -Path (Join-Path $modFolder "Ember_ReadMe.md") -Destination (Join-Path $SnapshotRoot "Ember_ReadMe.md")

    $sourceNames = Get-ChildItem -LiteralPath $configFolder -Filter *.lua | Sort-Object Name | ForEach-Object { $_.Name }
    $snapshotNames = Get-ChildItem -LiteralPath $snapshotConfig -Filter *.lua | Sort-Object Name | ForEach-Object { $_.Name }
    if (($sourceNames -join "|") -ne ($snapshotNames -join "|")) {
        throw "Snapshot config file names do not match source config file names."
    }

    foreach ($name in $sourceNames) {
        $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $configFolder $name)).Hash
        $snapshotHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $snapshotConfig $name)).Hash
        if ($sourceHash -ne $snapshotHash) {
            throw "Snapshot config hash mismatch: $name"
        }
    }

    foreach ($required in @("mod.lua", "User_Config_Overrides_Template.lua", "Ember_ReadMe.md")) {
        $snapshotAsset = Join-Path $SnapshotRoot $required
        if (-not (Test-Path -LiteralPath $snapshotAsset -PathType Leaf)) {
            throw "Required snapshot asset missing: $required"
        }

        $sourceAsset = switch ($required) {
            "mod.lua" { $modLua }
            "User_Config_Overrides_Template.lua" { Join-Path $modFolder "src\User_Config_Overrides_Template.lua" }
            "Ember_ReadMe.md" { Join-Path $modFolder "Ember_ReadMe.md" }
        }
        $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourceAsset).Hash
        $snapshotHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $snapshotAsset).Hash
        if ($sourceHash -ne $snapshotHash) {
            throw "Snapshot asset hash mismatch: $required"
        }
    }
}

Invoke-Checked { dotnet build $Project --configuration $Configuration } "dotnet build"

if ($Test) {
    Invoke-Checked { dotnet run --project $Tests --configuration $Configuration } "dotnet test harness"
}

if ($Publish) {
    $publishOutput = Join-Path $ArtifactRoot $Runtime
    if (Test-Path -LiteralPath $publishOutput) {
        $resolvedArtifactRoot = if (Test-Path -LiteralPath $ArtifactRoot) {
            (Resolve-Path -LiteralPath $ArtifactRoot).Path
        } else {
            New-Item -ItemType Directory -Force -Path $ArtifactRoot | Out-Null
            (Resolve-Path -LiteralPath $ArtifactRoot).Path
        }
        $resolvedPublishOutput = (Resolve-Path -LiteralPath $publishOutput).Path
        if (-not $resolvedPublishOutput.StartsWith($resolvedArtifactRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clear publish output outside artifact root: $resolvedPublishOutput"
        }

        Remove-Item -LiteralPath $resolvedPublishOutput -Recurse -Force
    }

    Invoke-Checked { dotnet publish $Project `
        --configuration $Configuration `
        --runtime $Runtime `
        --self-contained true `
        -p:UseAppHost=true `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=none `
        -p:DebugSymbols=false `
        -o $publishOutput } "dotnet publish"

    $resolvedPublishOutput = (Resolve-Path -LiteralPath $publishOutput).Path
    $publishRootPrefix = $resolvedPublishOutput.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $checksumPath = Join-Path $resolvedPublishOutput "SHA256SUMS.txt"
    $checksumLines = @(
        Get-ChildItem -LiteralPath $resolvedPublishOutput -File -Recurse |
            Where-Object { -not $_.FullName.Equals($checksumPath, [System.StringComparison]::OrdinalIgnoreCase) } |
            Sort-Object FullName |
            ForEach-Object {
                $relativePath = $_.FullName.Substring($publishRootPrefix.Length).Replace("\", "/")
                $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
                "$hash  $relativePath"
            }
    )
    Set-Content -LiteralPath $checksumPath -Value $checksumLines -Encoding ascii
}
