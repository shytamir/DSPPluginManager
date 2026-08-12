[CmdletBinding()]
param(
    [ValidateRange(1, [int]::MaxValue)]
    [int]$Sequence = 1,

    [ValidatePattern('^$|^[0-9a-fA-F]{7,40}$')]
    [string]$Commit = '',

    [string]$UnityEngineCoreModulePath = '',

    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Restore-ManagedDependencies.ps1') `
    -RepositoryRoot $RepositoryRoot
& (Join-Path $PSScriptRoot 'Restore-UnityDoorstop.ps1') `
    -RepositoryRoot $RepositoryRoot

if ([string]::IsNullOrWhiteSpace($Commit)) {
    $gitSafeRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path.Replace(
        '\',
        '/'
    )
    $Commit = (git -c "safe.directory=$gitSafeRoot" `
            -C $RepositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $Commit -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'Could not resolve a full source commit. Pass -Commit explicitly.'
    }
}

$version = & (Join-Path $PSScriptRoot 'Set-BuildVersion.ps1') `
    -Sequence $Sequence `
    -Commit $Commit `
    -RepositoryRoot $RepositoryRoot

$dllPath = Join-Path $RepositoryRoot `
    'artifacts\build\DSPPluginManager.dll'
$buildInfoPath = Join-Path $RepositoryRoot 'artifacts\BUILD-INFO.txt'
& (Join-Path $PSScriptRoot 'Build-Product.ps1') `
    -SemanticVersion $version.SEMANTIC_VERSION `
    -AssemblyVersion $version.ASSEMBLY_VERSION `
    -ReleaseLabel $version.RELEASE_LABEL `
    -UnityEngineCoreModulePath $UnityEngineCoreModulePath `
    -RepositoryRoot $RepositoryRoot
& (Join-Path $PSScriptRoot 'Test-BuildVersion.ps1') `
    -DllPath $dllPath `
    -BuildInfoPath $buildInfoPath `
    -ExpectedPackageVersion $version.PACKAGE_VERSION `
    -ExpectedSemanticVersion $version.SEMANTIC_VERSION `
    -ExpectedAssemblyVersion $version.ASSEMBLY_VERSION `
    -ExpectedReleaseLabel $version.RELEASE_LABEL `
    -ExpectedCommit $Commit `
    -ExpectedSequence $Sequence

$migrationKitPath = & (
    Join-Path $PSScriptRoot 'New-MigrationReferenceKit.ps1'
) -RepositoryRoot $RepositoryRoot
& (Join-Path $PSScriptRoot 'Test-MigrationReferenceKit.ps1') `
    -KitRoot $migrationKitPath `
    -ExpectedCommit $Commit `
    -ExpectedSequence $Sequence `
    -RepositoryRoot $RepositoryRoot

$packagePath = & (Join-Path $PSScriptRoot 'New-ThunderstorePackage.ps1') `
    -DllPath $dllPath `
    -BuildInfoPath $buildInfoPath `
    -VersionNumber $version.PACKAGE_VERSION `
    -RepositoryRoot $RepositoryRoot
& (Join-Path $PSScriptRoot 'Test-ThunderstorePackage.ps1') `
    -PackagePath $packagePath `
    -ExpectedVersion $version.PACKAGE_VERSION `
    -ExpectedDllPath $dllPath `
    -ExpectedBuildInfoPath $buildInfoPath `
    -ExpectedAssemblyVersion $version.ASSEMBLY_VERSION `
    -ExpectedReleaseLabel $version.RELEASE_LABEL `
    -RepositoryRoot $RepositoryRoot

$bundlePath = & (Join-Path $PSScriptRoot 'New-BootstrapBundle.ps1') `
    -RepositoryRoot $RepositoryRoot
& (Join-Path $PSScriptRoot 'Test-BootstrapBundle.ps1') `
    -RepositoryRoot $RepositoryRoot `
    -BundleRoot $bundlePath

Write-Output "Package build passed: $packagePath"
