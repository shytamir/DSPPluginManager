[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$SemanticVersion,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+\.0$')]
    [string]$AssemblyVersion,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+\.[0-9a-f]{7}$')]
    [string]$ReleaseLabel,

    [string]$RepositoryRoot = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$productProject = Join-Path $RepositoryRoot `
    'src\DSPPluginManager\DSPPluginManager.csproj'
$testProject = Join-Path $RepositoryRoot `
    'tests\DSPPluginManager.Tests\DSPPluginManager.Tests.csproj'
$packageDirectory = Join-Path $RepositoryRoot 'artifacts\nuget\packages'
$productOutput = Join-Path $RepositoryRoot 'artifacts\build'
$testOutput = Join-Path $RepositoryRoot 'artifacts\tests'

foreach ($project in @($productProject, $testProject)) {
    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
        throw "Required project was not found: $project"
    }
}
foreach ($lockFile in @(
        (Join-Path (Split-Path -Parent $productProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $testProject) 'packages.lock.json')
    )) {
    if (-not (Test-Path -LiteralPath $lockFile -PathType Leaf)) {
        throw "Required package lock was not found: $lockFile"
    }
}

$properties = @(
    "-p:Version=$SemanticVersion",
    "-p:AssemblyVersion=$AssemblyVersion",
    "-p:FileVersion=$AssemblyVersion",
    "-p:InformationalVersion=$ReleaseLabel",
    '-p:IncludeSourceRevisionInInformationalVersion=false'
)

Push-Location $RepositoryRoot
try {
    $sdkConfig = Get-Content -Raw -LiteralPath 'global.json' | ConvertFrom-Json
    $actualSdk = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or $actualSdk -cne $sdkConfig.sdk.version) {
        throw "Expected .NET SDK $($sdkConfig.sdk.version); found $actualSdk."
    }

    & dotnet restore $testProject `
        --packages $packageDirectory `
        --locked-mode `
        --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'Product restore failed.'
    }

    & dotnet build $productProject `
        --no-restore `
        --configuration Release `
        --output $productOutput `
        @properties
    if ($LASTEXITCODE -ne 0) {
        throw 'Product build failed.'
    }

    & dotnet build $testProject `
        --no-restore `
        --configuration Release `
        --output $testOutput `
        @properties
    if ($LASTEXITCODE -ne 0) {
        throw 'Foundation test build failed.'
    }
}
finally {
    Pop-Location
}

$productDll = Join-Path $productOutput 'DSPPluginManager.dll'
$testExecutable = Join-Path $testOutput 'DSPPluginManager.Tests.exe'
& $testExecutable $productDll $AssemblyVersion $ReleaseLabel
if ($LASTEXITCODE -ne 0) {
    throw 'Compiled foundation tests failed.'
}

Write-Output "Compiled product build passed: $productDll"
