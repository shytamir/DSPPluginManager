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

    [string]$UnityEngineCoreModulePath = '',

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
$contractTestProject = Join-Path $RepositoryRoot `
    'tests\DSPPluginManager.ContractTests\DSPPluginManager.ContractTests.csproj'
$handoffProject = Join-Path $RepositoryRoot `
    'src\DSPPluginManager.UnityHandoff\DSPPluginManager.UnityHandoff.csproj'
$contractProject = Join-Path $RepositoryRoot `
    'src\DSPPluginManager.Contracts\DSPPluginManager.Contracts.csproj'
$facadeProject = Join-Path $RepositoryRoot `
    'fixtures\UnityReferenceFacade\UnityReferenceFacade.csproj'
$consumerProject = Join-Path $RepositoryRoot `
    'fixtures\RM09.Consumer\DSPPluginManager.RM09Consumer.csproj'
$packageDirectory = Join-Path $RepositoryRoot 'artifacts\nuget\packages'
$productOutput = Join-Path $RepositoryRoot 'artifacts\build'
$testOutput = Join-Path $RepositoryRoot 'artifacts\tests'
$contractTestOutput = Join-Path $RepositoryRoot 'artifacts\contract-tests'
$handoffOutput = Join-Path $RepositoryRoot 'artifacts\bootstrap-components'
$contractOutput = Join-Path $RepositoryRoot 'artifacts\contracts'
$facadeOutput = Join-Path $RepositoryRoot `
    'artifacts\fixtures\unity-reference'
$consumerOutput = Join-Path $RepositoryRoot `
    'artifacts\fixtures\rm09-consumer'
$dependencyRuntime = Join-Path $RepositoryRoot `
    'artifacts\managed-dependencies\runtime'
$cecilReference = Join-Path $dependencyRuntime 'Mono.Cecil.dll'

foreach ($project in @(
        $productProject,
        $testProject,
        $contractTestProject,
        $handoffProject,
        $contractProject,
        $facadeProject,
        $consumerProject
    )) {
    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
        throw "Required project was not found: $project"
    }
}
foreach ($lockFile in @(
        (Join-Path (Split-Path -Parent $productProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $testProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $contractTestProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $handoffProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $contractProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $facadeProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $consumerProject) 'packages.lock.json')
    )) {
    if (-not (Test-Path -LiteralPath $lockFile -PathType Leaf)) {
        throw "Required package lock was not found: $lockFile"
    }
}
if (-not (Test-Path -LiteralPath $dependencyRuntime -PathType Container)) {
    throw "Validated managed dependency directory was not found: $dependencyRuntime"
}

$properties = @(
    "-p:Version=$SemanticVersion",
    "-p:AssemblyVersion=$AssemblyVersion",
    "-p:FileVersion=$AssemblyVersion",
    "-p:InformationalVersion=$ReleaseLabel",
    '-p:IncludeSourceRevisionInInformationalVersion=false'
)
$facadeProperties = @(
    '-p:Version=0.0.0',
    '-p:AssemblyVersion=0.0.0.0',
    '-p:FileVersion=0.0.0.0',
    '-p:InformationalVersion=compile-only',
    '-p:IncludeSourceRevisionInInformationalVersion=false'
)

Push-Location $RepositoryRoot
try {
    $sdkConfig = Get-Content -Raw -LiteralPath 'global.json' | ConvertFrom-Json
    $actualSdk = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or $actualSdk -cne $sdkConfig.sdk.version) {
        throw "Expected .NET SDK $($sdkConfig.sdk.version); found $actualSdk."
    }

    if ([string]::IsNullOrWhiteSpace($UnityEngineCoreModulePath)) {
        & dotnet restore $facadeProject `
            --packages $packageDirectory `
            --locked-mode `
            --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            throw 'Unity compile-reference facade restore failed.'
        }
        & dotnet build $facadeProject `
            --no-restore `
            --configuration Release `
            --output $facadeOutput `
            @facadeProperties
        if ($LASTEXITCODE -ne 0) {
            throw 'Unity compile-reference facade build failed.'
        }
        $UnityEngineCoreModulePath = Join-Path $facadeOutput `
            'UnityEngine.CoreModule.dll'
    }
    $UnityEngineCoreModulePath = (
        Resolve-Path -LiteralPath $UnityEngineCoreModulePath
    ).Path
    $unityIdentity = [Reflection.AssemblyName]::GetAssemblyName(
        $UnityEngineCoreModulePath
    )
    $unityToken = $unityIdentity.GetPublicKeyToken()
    if ($unityIdentity.Name -cne 'UnityEngine.CoreModule' -or
        $unityIdentity.Version.ToString() -cne '0.0.0.0' -or
        ($null -ne $unityToken -and $unityToken.Length -ne 0)) {
        throw 'Unity compile reference must be the neutral unsigned UnityEngine.CoreModule 0.0.0.0 identity.'
    }

    & dotnet restore $testProject `
        --packages $packageDirectory `
        --locked-mode `
        "-p:CecilReferencePath=$cecilReference" `
        --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'Product restore failed.'
    }
    & dotnet restore $contractTestProject `
        --packages $packageDirectory `
        --locked-mode `
        "-p:CecilReferencePath=$cecilReference" `
        --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'RM-09 contract test restore failed.'
    }
    & dotnet restore $handoffProject `
        --packages $packageDirectory `
        --locked-mode `
        "-p:CecilReferencePath=$cecilReference" `
        --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'Unity handoff restore failed.'
    }
    & dotnet restore $contractProject `
        --packages $packageDirectory `
        --locked-mode `
        "-p:UnityEngineCoreModulePath=$UnityEngineCoreModulePath" `
        --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'Plugin contract restore failed.'
    }
    & dotnet restore $consumerProject `
        --packages $packageDirectory `
        --locked-mode `
        "-p:UnityEngineCoreModulePath=$UnityEngineCoreModulePath" `
        "-p:PluginContractReferencePath=$(Join-Path $contractOutput 'DSPPluginManager.Contracts.dll')" `
        --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'RM-09 consumer fixture restore failed.'
    }

    & dotnet build $productProject `
        --no-restore `
        --configuration Release `
        --output $productOutput `
        "-p:CecilReferencePath=$cecilReference" `
        @properties
    if ($LASTEXITCODE -ne 0) {
        throw 'Product build failed.'
    }

    & dotnet build $contractProject `
        --no-restore `
        --configuration Release `
        --output $contractOutput `
        "-p:UnityEngineCoreModulePath=$UnityEngineCoreModulePath" `
        @properties
    if ($LASTEXITCODE -ne 0) {
        throw 'Plugin contract build failed.'
    }

    $contractDll = Join-Path $contractOutput `
        'DSPPluginManager.Contracts.dll'
    & dotnet build $consumerProject `
        --no-restore `
        --configuration Release `
        --output $consumerOutput `
        "-p:UnityEngineCoreModulePath=$UnityEngineCoreModulePath" `
        "-p:PluginContractReferencePath=$contractDll" `
        @properties
    if ($LASTEXITCODE -ne 0) {
        throw 'RM-09 consumer fixture build failed.'
    }

    & dotnet build $testProject `
        --no-restore `
        --configuration Release `
        --output $testOutput `
        "-p:CecilReferencePath=$cecilReference" `
        @properties
    if ($LASTEXITCODE -ne 0) {
        throw 'Foundation test build failed.'
    }

    & dotnet build $contractTestProject `
        --no-restore `
        --configuration Release `
        --output $contractTestOutput `
        "-p:CecilReferencePath=$cecilReference" `
        @properties
    if ($LASTEXITCODE -ne 0) {
        throw 'RM-09 contract test build failed.'
    }

    & dotnet build $handoffProject `
        --no-restore `
        --configuration Release `
        --output $handoffOutput `
        "-p:CecilReferencePath=$cecilReference" `
        @properties
    if ($LASTEXITCODE -ne 0) {
        throw 'Unity handoff component build failed.'
    }
}
finally {
    Pop-Location
}

$productDll = Join-Path $productOutput 'DSPPluginManager.dll'
$contractDll = Join-Path $contractOutput 'DSPPluginManager.Contracts.dll'
$consumerDll = Join-Path $consumerOutput `
    'DSPPluginManager.RM09Consumer.dll'
$staleTestCecil = Join-Path $testOutput 'Mono.Cecil.dll'
if (Test-Path -LiteralPath $staleTestCecil -PathType Leaf) {
    Remove-Item -LiteralPath $staleTestCecil -Force
}
foreach ($outputDirectory in @($contractOutput, $consumerOutput)) {
    if (Test-Path -LiteralPath (
            Join-Path $outputDirectory 'UnityEngine.CoreModule.dll'
        )) {
        throw "Unity compile input leaked into build output: $outputDirectory"
    }
}
$testExecutable = Join-Path $testOutput 'DSPPluginManager.Tests.exe'
& $testExecutable `
    $productDll `
    $AssemblyVersion `
    $ReleaseLabel `
    $dependencyRuntime
if ($LASTEXITCODE -ne 0) {
    throw 'Compiled product tests failed.'
}
$contractTestExecutable = Join-Path $contractTestOutput `
    'DSPPluginManager.ContractTests.exe'
$gameManagedDirectory = Split-Path -Parent $UnityEngineCoreModulePath
$runtimeFixtureOutput = Join-Path $contractTestOutput `
    'rm14-runtime-fixtures'
try {
    & $contractTestExecutable `
        $contractDll `
        $consumerDll `
        $AssemblyVersion `
        $dependencyRuntime `
        $gameManagedDirectory
    if ($LASTEXITCODE -ne 0) {
        throw 'Contract, static metadata, and runtime-loader tests failed.'
    }
}
finally {
    if (Test-Path -LiteralPath $runtimeFixtureOutput) {
        Remove-Item -LiteralPath $runtimeFixtureOutput -Recurse -Force
    }
}

Write-Output "Compiled product build passed: $productDll"
