[CmdletBinding()]
param(
    [string]$RepositoryRoot = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$repositoryPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$artifactsRoot = Join-Path $repositoryPath 'artifacts'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $artifactsRoot 'migration-reference-kit'
}
$outputPath = [IO.Path]::GetFullPath($OutputRoot).TrimEnd('\')
$allowedParent = [IO.Path]::GetFullPath($artifactsRoot).TrimEnd('\')
if (-not $outputPath.StartsWith(
        $allowedParent + '\',
        [StringComparison]::OrdinalIgnoreCase
    )) {
    throw 'Migration-reference kit output must remain under artifacts.'
}

$buildInfoPath = Join-Path $artifactsRoot 'BUILD-INFO.txt'
$contractPath = Join-Path $artifactsRoot `
    'contracts\DSPPluginManager.Contracts.dll'
$harmonyPath = Join-Path $artifactsRoot `
    'managed-dependencies\plugin-references\0Harmony.dll'
$noticeSource = Join-Path $artifactsRoot 'managed-dependencies\notices'
$instructionsPath = Join-Path $repositoryPath 'docs\MIGRATION.md'
foreach ($required in @(
        $buildInfoPath,
        $contractPath,
        $harmonyPath,
        $instructionsPath
    )) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Migration-reference kit input is missing: $required"
    }
}

$buildInfo = @{}
foreach ($line in Get-Content -LiteralPath $buildInfoPath) {
    if ($line -match '^([^:]+):\s*(.+)$') {
        $buildInfo[$Matches[1]] = $Matches[2]
    }
}
foreach ($name in @(
        'Release label',
        'Package version',
        'Assembly version',
        'Source commit',
        'Workflow sequence'
    )) {
    if (-not $buildInfo.ContainsKey($name)) {
        throw "Build information is missing '$name'."
    }
}
if ($buildInfo['Source commit'] -notmatch '^[0-9a-f]{40}$' -or
    $buildInfo['Workflow sequence'] -notmatch '^[1-9][0-9]*$') {
    throw 'Build information does not contain a reproducible revision/sequence.'
}

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}
$noticesPath = Join-Path $outputPath 'notices'
New-Item -ItemType Directory -Force -Path $noticesPath | Out-Null
Copy-Item -LiteralPath $contractPath -Destination $outputPath
Copy-Item -LiteralPath $harmonyPath -Destination $outputPath
Copy-Item -LiteralPath $instructionsPath `
    -Destination (Join-Path $outputPath 'MIGRATION.md')
Get-ChildItem -LiteralPath $noticeSource -File |
    Sort-Object Name |
    Copy-Item -Destination $noticesPath

$payloadFiles = @(Get-ChildItem -LiteralPath $outputPath -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
        $relative = ($_.FullName.Substring(
            $outputPath.Length + 1
        )).Replace('\', '/')
        [ordered]@{
            path = $relative
            length = $_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName `
                -Algorithm SHA256).Hash
        }
    })
$contractIdentity = [Reflection.AssemblyName]::GetAssemblyName(
    (Join-Path $outputPath 'DSPPluginManager.Contracts.dll')
)
$harmonyIdentity = [Reflection.AssemblyName]::GetAssemblyName(
    (Join-Path $outputPath '0Harmony.dll')
)
$integrity = [ordered]@{
    schemaVersion = 1
    purpose = 'compile-reference'
    managerRevision = $buildInfo['Source commit']
    workflowSequence = [int]$buildInfo['Workflow sequence']
    packageVersion = $buildInfo['Package version']
    releaseLabel = $buildInfo['Release label']
    contract = [ordered]@{
        assemblyName = $contractIdentity.Name
        assemblyVersion = $contractIdentity.Version.ToString()
    }
    harmony = [ordered]@{
        assemblyName = $harmonyIdentity.Name
        assemblyVersion = $harmonyIdentity.Version.ToString()
    }
    files = $payloadFiles
}
[IO.File]::WriteAllText(
    (Join-Path $outputPath 'INTEGRITY.json'),
    ($integrity | ConvertTo-Json -Depth 5),
    (New-Object Text.UTF8Encoding($false))
)

Write-Output $outputPath
