[CmdletBinding()]
param(
    [string]$RepositoryRoot = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$repositoryPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$artifactsRoot = Join-Path $repositoryPath 'artifacts'
$bundleRoot = Join-Path $artifactsRoot 'bootstrap-bundle'
$managerRoot = Join-Path $bundleRoot 'DSPPluginManager'
$dependencyRoot = Join-Path $managerRoot 'dependencies'
$noticeRoot = Join-Path $managerRoot 'notices'

if (Test-Path -LiteralPath $bundleRoot) {
    Remove-Item -LiteralPath $bundleRoot -Recurse -Force
}
foreach ($directory in @(
        $bundleRoot,
        $managerRoot,
        $dependencyRoot,
        $noticeRoot
    )) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

Copy-Item -LiteralPath (
    Join-Path $artifactsRoot 'unitydoorstop\runtime\winhttp.dll'
) -Destination $bundleRoot
Copy-Item -LiteralPath (
    Join-Path $artifactsRoot 'build\DSPPluginManager.dll'
) -Destination $managerRoot
Copy-Item -LiteralPath (
    Join-Path $artifactsRoot 'contracts\DSPPluginManager.Contracts.dll'
) -Destination $managerRoot
Copy-Item -LiteralPath (
    Join-Path $artifactsRoot `
        'bootstrap-components\DSPPluginManager.UnityHandoff.dll'
) -Destination $managerRoot
Get-ChildItem -LiteralPath (
    Join-Path $artifactsRoot 'managed-dependencies\runtime'
) -File | Copy-Item -Destination $dependencyRoot
Get-ChildItem -LiteralPath (
    Join-Path $artifactsRoot 'managed-dependencies\notices'
) -File | Copy-Item -Destination $noticeRoot
Get-ChildItem -LiteralPath (
    Join-Path $artifactsRoot 'unitydoorstop\notices'
) -File | Copy-Item -Destination $noticeRoot

$config = @'
# DSP Plugin Manager owns this configuration.
[UnityDoorstop]
enabled=true
targetAssembly=DSPPluginManager\DSPPluginManager.dll
redirectOutputLog=false
ignoreDisableSwitch=false
dllSearchPathOverride=

[MonoBackend]
runtimeLib=
configDir=
corlibDir=
debugEnabled=false
debugSuspend=false
debugAddress=127.0.0.1:10000
'@
Set-Content -LiteralPath (Join-Path $bundleRoot 'doorstop_config.ini') `
    -Value $config -Encoding ascii

Write-Output $bundleRoot
