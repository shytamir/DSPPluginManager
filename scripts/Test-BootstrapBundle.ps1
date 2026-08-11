[CmdletBinding()]
param(
    [string]$RepositoryRoot = '',
    [string]$BundleRoot = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$repositoryPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
if ([string]::IsNullOrWhiteSpace($BundleRoot)) {
    $BundleRoot = Join-Path $repositoryPath 'artifacts\bootstrap-bundle'
}
$bundlePath = (Resolve-Path -LiteralPath $BundleRoot).Path
$managerRoot = Join-Path $bundlePath 'DSPPluginManager'
$requiredFiles = @(
    (Join-Path $bundlePath 'winhttp.dll'),
    (Join-Path $bundlePath 'doorstop_config.ini'),
    (Join-Path $managerRoot 'DSPPluginManager.dll'),
    (Join-Path $managerRoot 'DSPPluginManager.Contracts.dll'),
    (Join-Path $managerRoot 'DSPPluginManager.UnityHandoff.dll'),
    (Join-Path $managerRoot 'dependencies\0Harmony.dll'),
    (Join-Path $managerRoot 'dependencies\Mono.Cecil.dll'),
    (Join-Path $managerRoot 'dependencies\MonoMod.RuntimeDetour.dll'),
    (Join-Path $managerRoot 'dependencies\MonoMod.Utils.dll'),
    (Join-Path $managerRoot 'notices\UnityDoorstop-CC0.txt')
)
foreach ($path in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Bootstrap bundle file is missing: $path"
    }
}
$lock = Get-Content -Raw -LiteralPath (
    Join-Path $repositoryPath 'dependencies\unitydoorstop.lock.json'
) | ConvertFrom-Json
$proxy = Join-Path $bundlePath 'winhttp.dll'
if ((Get-Item -LiteralPath $proxy).Length -ne $lock.proxy.length -or
    (Get-FileHash -LiteralPath $proxy -Algorithm SHA256).Hash -cne
        $lock.proxy.sha256) {
    throw 'Bootstrap bundle proxy does not match the UnityDoorstop lock.'
}

$configLines = [IO.File]::ReadAllLines(
    (Join-Path $bundlePath 'doorstop_config.ini')
)
foreach ($requiredLine in @(
        'enabled=true',
        'targetAssembly=DSPPluginManager\DSPPluginManager.dll',
        'redirectOutputLog=false',
        'ignoreDisableSwitch=false'
    )) {
    if ($configLines -cnotcontains $requiredLine) {
        throw "Doorstop configuration is missing '$requiredLine'."
    }
}

$builtProduct = Join-Path $repositoryPath `
    'artifacts\build\DSPPluginManager.dll'
$bundledProduct = Join-Path $managerRoot 'DSPPluginManager.dll'
if ((Get-FileHash -LiteralPath $bundledProduct -Algorithm SHA256).Hash -cne
    (Get-FileHash -LiteralPath $builtProduct -Algorithm SHA256).Hash) {
    throw 'The bundled entry assembly differs from the tested product build.'
}
$builtContract = Join-Path $repositoryPath `
    'artifacts\contracts\DSPPluginManager.Contracts.dll'
$bundledContract = Join-Path $managerRoot `
    'DSPPluginManager.Contracts.dll'
if ((Get-FileHash -LiteralPath $bundledContract -Algorithm SHA256).Hash -cne
    (Get-FileHash -LiteralPath $builtContract -Algorithm SHA256).Hash) {
    throw 'The bundled plugin contract differs from the tested contract build.'
}
if (@(Get-ChildItem -LiteralPath $bundlePath -Recurse -File |
        Where-Object { $_.Name -ceq 'UnityEngine.CoreModule.dll' }).Count -ne 0) {
    throw 'A Unity compile reference leaked into the bootstrap bundle.'
}

Write-Output "Bootstrap bundle validation passed: $bundlePath"
