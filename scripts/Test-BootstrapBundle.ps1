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

$product = [Reflection.Assembly]::ReflectionOnlyLoadFrom(
    (Join-Path $managerRoot 'DSPPluginManager.dll')
)
$entryType = $product.GetType(
    'DSPPluginManager.Bootstrap.DoorstopEntrypoint',
    $true,
    $false
)
$mainMethods = @($entryType.GetMethods(
        [Reflection.BindingFlags]'Public,Static,DeclaredOnly'
    ) | Where-Object {
        $_.Name -ceq 'Main' -and
        $_.ReturnType.FullName -ceq 'System.Void' -and
        $_.GetParameters().Count -eq 0
    })
if ($mainMethods.Count -ne 1) {
    throw "Manager bundle must expose exactly one public parameterless Main."
}

Write-Output "Bootstrap bundle validation passed: $bundlePath"
