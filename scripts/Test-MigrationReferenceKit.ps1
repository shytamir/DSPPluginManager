[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$KitRoot,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$ExpectedCommit,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$ExpectedSequence,

    [string]$RepositoryRoot = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$repositoryPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$kitPath = (Resolve-Path -LiteralPath $KitRoot).Path
$integrityPath = Join-Path $kitPath 'INTEGRITY.json'
if (-not (Test-Path -LiteralPath $integrityPath -PathType Leaf)) {
    throw 'Migration-reference kit integrity metadata is missing.'
}
$integrity = Get-Content -Raw -LiteralPath $integrityPath | ConvertFrom-Json
if ($integrity.schemaVersion -ne 1 -or
    $integrity.purpose -cne 'compile-reference' -or
    $integrity.managerRevision -cne $ExpectedCommit.ToLowerInvariant() -or
    $integrity.workflowSequence -ne $ExpectedSequence) {
    throw 'Migration-reference kit provenance does not match the requested build.'
}

$lock = Get-Content -Raw -LiteralPath (
    Join-Path $repositoryPath 'dependencies\managed-dependencies.lock.json'
) | ConvertFrom-Json
$noticeNames = @($lock.notices | ForEach-Object { $_.file } | Sort-Object)
$expectedFiles = @(
    '0Harmony.dll',
    'DSPPluginManager.Contracts.dll',
    'INTEGRITY.json',
    'MIGRATION.md'
) + @($noticeNames | ForEach-Object { 'notices/' + $_ })
$actualFiles = @(Get-ChildItem -LiteralPath $kitPath -Recurse -File |
    ForEach-Object {
        $_.FullName.Substring($kitPath.Length + 1).Replace('\', '/')
    } | Sort-Object)
if (($actualFiles -join "`n") -cne (($expectedFiles | Sort-Object) -join "`n")) {
    throw "Migration-reference kit file set is invalid: $($actualFiles -join ', ')"
}

$metadataFiles = @($integrity.files)
if ($metadataFiles.Count -ne $expectedFiles.Count - 1) {
    throw 'Migration-reference kit integrity file count is invalid.'
}
$expectedMetadataPaths = @($expectedFiles |
    Where-Object { $_ -cne 'INTEGRITY.json' } |
    Sort-Object)
$metadataPaths = @($metadataFiles.path | Sort-Object)
if (($metadataPaths -join "`n") -cne ($expectedMetadataPaths -join "`n")) {
    throw 'Migration-reference kit integrity metadata does not cover its exact payload.'
}
foreach ($file in $metadataFiles) {
    $path = Join-Path $kitPath $file.path.Replace('/', '\')
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
        (Get-Item -LiteralPath $path).Length -ne $file.length -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne
            $file.sha256) {
        throw "Migration-reference kit integrity failed: $($file.path)"
    }
}

$builtContract = Join-Path $repositoryPath `
    'artifacts\contracts\DSPPluginManager.Contracts.dll'
$kitContract = Join-Path $kitPath 'DSPPluginManager.Contracts.dll'
$approvedHarmony = Join-Path $repositoryPath `
    'artifacts\managed-dependencies\plugin-references\0Harmony.dll'
$kitHarmony = Join-Path $kitPath '0Harmony.dll'
foreach ($pair in @(
        @($builtContract, $kitContract),
        @($approvedHarmony, $kitHarmony)
    )) {
    if ((Get-FileHash -LiteralPath $pair[0] -Algorithm SHA256).Hash -cne
        (Get-FileHash -LiteralPath $pair[1] -Algorithm SHA256).Hash) {
        throw "Migration-reference kit binary differs from its validated source: $($pair[1])"
    }
}
$contractIdentity = [Reflection.AssemblyName]::GetAssemblyName($kitContract)
$harmonyIdentity = [Reflection.AssemblyName]::GetAssemblyName($kitHarmony)
if ($contractIdentity.Name -cne 'DSPPluginManager.Contracts' -or
    $contractIdentity.Version.ToString() -cne
        $integrity.contract.assemblyVersion -or
    $harmonyIdentity.Name -cne '0Harmony' -or
    $harmonyIdentity.Version.ToString() -cne '2.5.5.0') {
    throw 'Migration-reference kit assembly identity is invalid.'
}

foreach ($notice in $lock.notices) {
    $noticePath = Join-Path $kitPath ('notices\' + $notice.file)
    if ((Get-FileHash -LiteralPath $noticePath -Algorithm SHA256).Hash -cne
        $notice.sha256) {
        throw "Migration-reference kit notice integrity failed: $($notice.file)"
    }
}
$sourceInstructions = Join-Path $repositoryPath 'docs\MIGRATION.md'
$kitInstructions = Join-Path $kitPath 'MIGRATION.md'
if ((Get-FileHash -LiteralPath $sourceInstructions -Algorithm SHA256).Hash -cne
    (Get-FileHash -LiteralPath $kitInstructions -Algorithm SHA256).Hash) {
    throw 'Migration instructions in the kit differ from the reviewed source.'
}
$instructions = Get-Content -Raw -LiteralPath $kitInstructions
foreach ($requiredText in @(
        'DSPPluginManager.Contracts.PluginAttribute',
        'DSPPluginManager.Contracts.PluginBehaviour',
        'PluginConfigurationEntry<bool>',
        'PluginConfigurationEntry<KeyboardShortcut>',
        'public override void Activate()',
        'public override void Deactivate()',
        'DSPPluginManager.Contracts.dll',
        '0Harmony.dll',
        'BepInEx.dll',
        'Mirror-first checklist',
        'Guide-following checklist',
        '**Blocked substitution:**'
    )) {
    if ($instructions.IndexOf($requiredText, [StringComparison]::Ordinal) -lt 0) {
        throw "Migration instructions are missing required contract text: $requiredText"
    }
}

$mirrorFixture = Join-Path $repositoryPath `
    'artifacts\fixtures\rm32-mirror-qualification\DSPPluginManager.RM32MirrorQualification.dll'
$guideFixture = Join-Path $repositoryPath `
    'artifacts\fixtures\rm32-guide-qualification\DSPPluginManager.RM32GuideQualification.dll'
$mirrorReferences = [Reflection.Assembly]::ReflectionOnlyLoadFrom(
    $mirrorFixture
).GetReferencedAssemblies().Name
$guideReferences = [Reflection.Assembly]::ReflectionOnlyLoadFrom(
    $guideFixture
).GetReferencedAssemblies().Name
if ($mirrorReferences -cnotcontains 'DSPPluginManager.Contracts' -or
    $mirrorReferences -cnotcontains '0Harmony' -or
    $guideReferences -cnotcontains 'DSPPluginManager.Contracts' -or
    $guideReferences -ccontains '0Harmony' -or
    @($mirrorReferences + $guideReferences | Where-Object {
            $_ -like 'BepInEx*'
        }).Count -ne 0) {
    throw 'RM-32 fixture references do not match the migration instructions.'
}

Write-Output "Migration-reference kit validation passed: $kitPath"
