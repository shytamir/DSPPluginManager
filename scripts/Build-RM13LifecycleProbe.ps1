[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameManagedDirectory,

    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
$repositoryPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$managedPath = (Resolve-Path -LiteralPath $GameManagedDirectory).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryPath 'artifacts\rm13-probe'
}
$outputPath = [IO.Path]::GetFullPath($OutputRoot)
$artifactsPath = [IO.Path]::GetFullPath(
    (Join-Path $repositoryPath 'artifacts')
).TrimEnd('\')
if (-not $outputPath.StartsWith(
        $artifactsPath + '\',
        [StringComparison]::OrdinalIgnoreCase
    )) {
    throw "RM-13 probe output must remain below '$artifactsPath'."
}

$productAssembly = Join-Path $repositoryPath `
    'artifacts\build\DSPPluginManager.dll'
$dependencyDirectory = Join-Path $repositoryPath `
    'artifacts\managed-dependencies\runtime'
$unityCore = Join-Path $managedPath 'UnityEngine.CoreModule.dll'
foreach ($requiredFile in @(
        $productAssembly,
        $unityCore,
        (Join-Path $dependencyDirectory 'Mono.Cecil.dll')
    )) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required RM-13 probe input was not found: $requiredFile"
    }
}

New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
$probeRoot = Join-Path $repositoryPath 'probes\RM13.Lifecycle'
$entryProject = Join-Path $probeRoot `
    'Entrypoint\DSPPluginManager.RM13Probe.csproj'
$callbackProject = Join-Path $probeRoot `
    'Callback\DSPPluginManager.RM13Callback.csproj'
$cecilProject = Join-Path $probeRoot `
    'CecilHandoff\DSPPluginManager.RM13CecilHandoff.csproj'
$packageDirectory = Join-Path $repositoryPath 'artifacts\nuget\packages'
$entryOutput = Join-Path $outputPath 'entry'
$callbackOutput = Join-Path $outputPath 'callback'
$cecilOutput = Join-Path $outputPath 'cecil'
$deployOutput = Join-Path $outputPath 'deploy'
foreach ($generatedPath in @(
        $entryOutput,
        $callbackOutput,
        $cecilOutput,
        $deployOutput
    )) {
    if (Test-Path -LiteralPath $generatedPath) {
        Remove-Item -LiteralPath $generatedPath -Recurse -Force
    }
}

$sdkConfig = Get-Content -Raw -LiteralPath (
    Join-Path $repositoryPath 'global.json'
) | ConvertFrom-Json
$actualSdk = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or $actualSdk -cne $sdkConfig.sdk.version) {
    throw "Expected .NET SDK $($sdkConfig.sdk.version); found $actualSdk."
}

& dotnet restore $entryProject `
    --packages $packageDirectory `
    -p:DSPPluginManagerAssembly=$productAssembly `
    --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    throw 'RM-13 entrypoint restore failed.'
}
& dotnet build $entryProject `
    --no-restore `
    --configuration Release `
    --output $entryOutput `
    -p:DSPPluginManagerAssembly=$productAssembly
if ($LASTEXITCODE -ne 0) {
    throw 'RM-13 entrypoint build failed.'
}

$entryAssembly = Join-Path $entryOutput 'DSPPluginManager.RM13Probe.dll'
& dotnet restore $callbackProject `
    --packages $packageDirectory `
    -p:RM13ProbeAssembly=$entryAssembly `
    -p:DSPManagedDirectory=$managedPath `
    --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    throw 'RM-13 callback restore failed.'
}
& dotnet build $callbackProject `
    --no-restore `
    --configuration Release `
    --output $callbackOutput `
    -p:RM13ProbeAssembly=$entryAssembly `
    -p:DSPManagedDirectory=$managedPath
if ($LASTEXITCODE -ne 0) {
    throw 'RM-13 callback build failed.'
}

& dotnet restore $cecilProject `
    --packages $packageDirectory `
    -p:ManagedDependencyDirectory=$dependencyDirectory `
    --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    throw 'RM-13 Cecil installer restore failed.'
}
& dotnet build $cecilProject `
    --no-restore `
    --configuration Release `
    --output $cecilOutput `
    -p:ManagedDependencyDirectory=$dependencyDirectory
if ($LASTEXITCODE -ne 0) {
    throw 'RM-13 Cecil installer build failed.'
}

New-Item -ItemType Directory -Path $deployOutput | Out-Null
New-Item -ItemType Directory -Path (
    Join-Path $deployOutput 'dependencies'
) | Out-Null
New-Item -ItemType Directory -Path (
    Join-Path $deployOutput 'plugins'
) | Out-Null
foreach ($source in @(
        $entryAssembly,
        (Join-Path $callbackOutput 'DSPPluginManager.RM13Callback.dll'),
        (Join-Path $cecilOutput 'DSPPluginManager.RM13CecilHandoff.dll'),
        $productAssembly
    )) {
    Copy-Item -LiteralPath $source -Destination $deployOutput
}
foreach ($fileName in @(
        '0Harmony.dll',
        'MonoMod.RuntimeDetour.dll',
        'MonoMod.Utils.dll',
        'Mono.Cecil.dll'
    )) {
    Copy-Item -LiteralPath (Join-Path $dependencyDirectory $fileName) `
        -Destination (Join-Path $deployOutput 'dependencies')
}

$buildInfo = @(
    'RM-13 disposable lifecycle observability probe',
    "Game managed directory: $managedPath",
    "UnityEngine.CoreModule SHA-256: $((Get-FileHash -Algorithm SHA256 -LiteralPath $unityCore).Hash)",
    "Product assembly SHA-256: $((Get-FileHash -Algorithm SHA256 -LiteralPath $productAssembly).Hash)",
    "Entry assembly SHA-256: $((Get-FileHash -Algorithm SHA256 -LiteralPath $entryAssembly).Hash)",
    "Callback assembly SHA-256: $((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $callbackOutput 'DSPPluginManager.RM13Callback.dll')).Hash)"
)
[IO.File]::WriteAllLines(
    (Join-Path $outputPath 'PROBE-BUILD-INFO.txt'),
    $buildInfo,
    (New-Object Text.UTF8Encoding($false))
)

Write-Output "RM-13 probe build passed: $deployOutput"
