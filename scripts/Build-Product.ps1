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
$harmonyTestProject = Join-Path $RepositoryRoot `
    'tests\DSPPluginManager.HarmonyTests\DSPPluginManager.HarmonyTests.csproj'
$handoffProject = Join-Path $RepositoryRoot `
    'src\DSPPluginManager.UnityHandoff\DSPPluginManager.UnityHandoff.csproj'
$unityHostProject = Join-Path $RepositoryRoot `
    'src\DSPPluginManager.UnityHost\DSPPluginManager.UnityHost.csproj'
$contractProject = Join-Path $RepositoryRoot `
    'src\DSPPluginManager.Contracts\DSPPluginManager.Contracts.csproj'
$facadeProject = Join-Path $RepositoryRoot `
    'fixtures\UnityReferenceFacade\UnityReferenceFacade.csproj'
$consumerProject = Join-Path $RepositoryRoot `
    'fixtures\RM09.Consumer\DSPPluginManager.RM09Consumer.csproj'
$constructionFailureProject = Join-Path $RepositoryRoot `
    'fixtures\RM20.ConstructionFailure\DSPPluginManager.RM20ConstructionFailure.csproj'
$activationFailureProject = Join-Path $RepositoryRoot `
    'fixtures\RM20.ActivationFailure\DSPPluginManager.RM20ActivationFailure.csproj'
$runtimeDeliveryProject = Join-Path $RepositoryRoot `
    'fixtures\RM21.RuntimeDelivery\DSPPluginManager.RM21RuntimeDelivery.csproj'
$cleanupFailureProject = Join-Path $RepositoryRoot `
    'fixtures\RM22.CleanupFailure\DSPPluginManager.RM22CleanupFailure.csproj'
$cleanupSuccessProject = Join-Path $RepositoryRoot `
    'fixtures\RM22.CleanupSuccess\DSPPluginManager.RM22CleanupSuccess.csproj'
$harmonyFailureProject = Join-Path $RepositoryRoot `
    'fixtures\RM23.HarmonyActivationFailure\DSPPluginManager.RM23HarmonyActivationFailure.csproj'
$harmonyLifecycleProject = Join-Path $RepositoryRoot `
    'fixtures\RM23.HarmonyLifecycle\DSPPluginManager.RM23HarmonyLifecycle.csproj'
$packageDirectory = Join-Path $RepositoryRoot 'artifacts\nuget\packages'
$productOutput = Join-Path $RepositoryRoot 'artifacts\build'
$testOutput = Join-Path $RepositoryRoot 'artifacts\tests'
$contractTestOutput = Join-Path $RepositoryRoot 'artifacts\contract-tests'
$harmonyTestOutput = Join-Path $RepositoryRoot 'artifacts\harmony-tests'
$handoffOutput = Join-Path $RepositoryRoot 'artifacts\bootstrap-components'
$contractOutput = Join-Path $RepositoryRoot 'artifacts\contracts'
$facadeOutput = Join-Path $RepositoryRoot `
    'artifacts\fixtures\unity-reference'
$consumerOutput = Join-Path $RepositoryRoot `
    'artifacts\fixtures\rm09-consumer'
$constructionFailureOutput = Join-Path $RepositoryRoot `
    'artifacts\fixtures\rm20-construction-failure'
$activationFailureOutput = Join-Path $RepositoryRoot `
    'artifacts\fixtures\rm20-activation-failure'
$runtimeDeliveryOutput = Join-Path $RepositoryRoot `
    'artifacts\fixtures\rm21-runtime-delivery'
$cleanupFailureOutput = Join-Path $RepositoryRoot `
    'artifacts\fixtures\rm22-cleanup-failure'
$cleanupSuccessOutput = Join-Path $RepositoryRoot `
    'artifacts\fixtures\rm22-cleanup-success'
$harmonyFailureOutput = Join-Path $RepositoryRoot `
    'artifacts\fixtures\rm23-harmony-activation-failure'
$harmonyLifecycleOutput = Join-Path $RepositoryRoot `
    'artifacts\fixtures\rm23-harmony-lifecycle'
$dependencyRuntime = Join-Path $RepositoryRoot `
    'artifacts\managed-dependencies\runtime'
$cecilReference = Join-Path $dependencyRuntime 'Mono.Cecil.dll'
$harmonyReference = Join-Path $RepositoryRoot `
    'artifacts\managed-dependencies\plugin-references\0Harmony.dll'

foreach ($project in @(
        $productProject,
        $testProject,
        $contractTestProject,
        $harmonyTestProject,
        $handoffProject,
        $unityHostProject,
        $contractProject,
        $facadeProject,
        $consumerProject,
        $constructionFailureProject,
        $activationFailureProject,
        $runtimeDeliveryProject,
        $cleanupFailureProject,
        $cleanupSuccessProject,
        $harmonyFailureProject,
        $harmonyLifecycleProject
    )) {
    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
        throw "Required project was not found: $project"
    }
}
foreach ($lockFile in @(
        (Join-Path (Split-Path -Parent $productProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $testProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $contractTestProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $harmonyTestProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $handoffProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $unityHostProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $contractProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $facadeProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $consumerProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $constructionFailureProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $activationFailureProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $runtimeDeliveryProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $cleanupFailureProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $cleanupSuccessProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $harmonyFailureProject) 'packages.lock.json'),
        (Join-Path (Split-Path -Parent $harmonyLifecycleProject) 'packages.lock.json')
    )) {
    if (-not (Test-Path -LiteralPath $lockFile -PathType Leaf)) {
        throw "Required package lock was not found: $lockFile"
    }
}
if (-not (Test-Path -LiteralPath $dependencyRuntime -PathType Container)) {
    throw "Validated managed dependency directory was not found: $dependencyRuntime"
}
if (-not (Test-Path -LiteralPath $harmonyReference -PathType Leaf)) {
    throw "Validated Harmony plugin reference was not found: $harmonyReference"
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
    $facadeDll = Join-Path $facadeOutput 'UnityEngine.CoreModule.dll'
    if ([string]::IsNullOrWhiteSpace($UnityEngineCoreModulePath)) {
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
    & dotnet restore $harmonyTestProject `
        --packages $packageDirectory `
        --locked-mode `
        "-p:CecilReferencePath=$cecilReference" `
        --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'RM-23 Harmony lifecycle test restore failed.'
    }
    & dotnet restore $handoffProject `
        --packages $packageDirectory `
        --locked-mode `
        "-p:CecilReferencePath=$cecilReference" `
        --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'Unity handoff restore failed.'
    }
    & dotnet restore $unityHostProject `
        --packages $packageDirectory `
        --locked-mode `
        "-p:DSPPluginManagerAssemblyPath=$(Join-Path $productOutput 'DSPPluginManager.dll')" `
        "-p:DSPPluginManagerContractsAssemblyPath=$(Join-Path $contractOutput 'DSPPluginManager.Contracts.dll')" `
        "-p:UnityEngineCoreModulePath=$UnityEngineCoreModulePath" `
        --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'Unity host restore failed.'
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
    foreach ($failureProject in @(
            $constructionFailureProject,
            $activationFailureProject
        )) {
        & dotnet restore $failureProject `
            --packages $packageDirectory `
            --locked-mode `
            "-p:UnityEngineCoreModulePath=$UnityEngineCoreModulePath" `
            "-p:PluginContractReferencePath=$(Join-Path $contractOutput 'DSPPluginManager.Contracts.dll')" `
            --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            throw "RM-20 failure fixture restore failed: $failureProject"
        }
    }
    & dotnet restore $runtimeDeliveryProject `
        --packages $packageDirectory `
        --locked-mode `
        "-p:UnityEngineCoreModulePath=$UnityEngineCoreModulePath" `
        "-p:PluginContractReferencePath=$(Join-Path $contractOutput 'DSPPluginManager.Contracts.dll')" `
        --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'RM-21 runtime-delivery fixture restore failed.'
    }
    foreach ($cleanupProject in @(
            $cleanupFailureProject,
            $cleanupSuccessProject
        )) {
        & dotnet restore $cleanupProject `
            --packages $packageDirectory `
            --locked-mode `
            "-p:UnityEngineCoreModulePath=$UnityEngineCoreModulePath" `
            "-p:PluginContractReferencePath=$(Join-Path $contractOutput 'DSPPluginManager.Contracts.dll')" `
            --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            throw "RM-22 cleanup fixture restore failed: $cleanupProject"
        }
    }
    foreach ($harmonyProject in @(
            $harmonyFailureProject,
            $harmonyLifecycleProject
        )) {
        & dotnet restore $harmonyProject `
            --packages $packageDirectory `
            --locked-mode `
            "-p:UnityEngineCoreModulePath=$UnityEngineCoreModulePath" `
            "-p:PluginContractReferencePath=$(Join-Path $contractOutput 'DSPPluginManager.Contracts.dll')" `
            "-p:HarmonyReferencePath=$harmonyReference" `
            --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            throw "RM-23 Harmony fixture restore failed: $harmonyProject"
        }
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

    $productDll = Join-Path $productOutput 'DSPPluginManager.dll'
    & dotnet build $unityHostProject `
        --no-restore `
        --configuration Release `
        --output $handoffOutput `
        "-p:DSPPluginManagerAssemblyPath=$productDll" `
        "-p:DSPPluginManagerContractsAssemblyPath=$(Join-Path $contractOutput 'DSPPluginManager.Contracts.dll')" `
        "-p:UnityEngineCoreModulePath=$UnityEngineCoreModulePath" `
        @properties
    if ($LASTEXITCODE -ne 0) {
        throw 'Persistent Unity host build failed.'
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
    $failureBuilds = @(
        [pscustomobject]@{
            Project = $constructionFailureProject
            Output = $constructionFailureOutput
        },
        [pscustomobject]@{
            Project = $activationFailureProject
            Output = $activationFailureOutput
        }
    )
    foreach ($failureBuild in $failureBuilds) {
        & dotnet build $failureBuild.Project `
            --no-restore `
            --configuration Release `
            --output $failureBuild.Output `
            "-p:UnityEngineCoreModulePath=$UnityEngineCoreModulePath" `
            "-p:PluginContractReferencePath=$contractDll" `
            @properties
        if ($LASTEXITCODE -ne 0) {
            throw "RM-20 failure fixture build failed: $($failureBuild.Project)"
        }
    }
    & dotnet build $runtimeDeliveryProject `
        --no-restore `
        --configuration Release `
        --output $runtimeDeliveryOutput `
        "-p:UnityEngineCoreModulePath=$UnityEngineCoreModulePath" `
        "-p:PluginContractReferencePath=$contractDll" `
        @properties
    if ($LASTEXITCODE -ne 0) {
        throw 'RM-21 runtime-delivery fixture build failed.'
    }
    $cleanupBuilds = @(
        [pscustomobject]@{
            Project = $cleanupFailureProject
            Output = $cleanupFailureOutput
        },
        [pscustomobject]@{
            Project = $cleanupSuccessProject
            Output = $cleanupSuccessOutput
        }
    )
    foreach ($cleanupBuild in $cleanupBuilds) {
        & dotnet build $cleanupBuild.Project `
            --no-restore `
            --configuration Release `
            --output $cleanupBuild.Output `
            "-p:UnityEngineCoreModulePath=$UnityEngineCoreModulePath" `
            "-p:PluginContractReferencePath=$contractDll" `
            @properties
        if ($LASTEXITCODE -ne 0) {
            throw "RM-22 cleanup fixture build failed: $($cleanupBuild.Project)"
        }
    }
    $harmonyBuilds = @(
        [pscustomobject]@{
            Project = $harmonyFailureProject
            Output = $harmonyFailureOutput
        },
        [pscustomobject]@{
            Project = $harmonyLifecycleProject
            Output = $harmonyLifecycleOutput
        }
    )
    foreach ($harmonyBuild in $harmonyBuilds) {
        & dotnet build $harmonyBuild.Project `
            --no-restore `
            --configuration Release `
            --output $harmonyBuild.Output `
            "-p:UnityEngineCoreModulePath=$UnityEngineCoreModulePath" `
            "-p:PluginContractReferencePath=$contractDll" `
            "-p:HarmonyReferencePath=$harmonyReference" `
            @properties
        if ($LASTEXITCODE -ne 0) {
            throw "RM-23 Harmony fixture build failed: $($harmonyBuild.Project)"
        }
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
    & dotnet build $harmonyTestProject `
        --no-restore `
        --configuration Release `
        --output $harmonyTestOutput `
        "-p:CecilReferencePath=$cecilReference" `
        @properties
    if ($LASTEXITCODE -ne 0) {
        throw 'RM-23 Harmony lifecycle test build failed.'
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
$unityHostDll = Join-Path $handoffOutput 'DSPPluginManager.UnityHost.dll'
$contractDll = Join-Path $contractOutput 'DSPPluginManager.Contracts.dll'
$consumerDll = Join-Path $consumerOutput `
    'DSPPluginManager.RM09Consumer.dll'
$constructionFailureDll = Join-Path $constructionFailureOutput `
    'DSPPluginManager.RM20ConstructionFailure.dll'
$activationFailureDll = Join-Path $activationFailureOutput `
    'DSPPluginManager.RM20ActivationFailure.dll'
$runtimeDeliveryDll = Join-Path $runtimeDeliveryOutput `
    'DSPPluginManager.RM21RuntimeDelivery.dll'
$cleanupFailureDll = Join-Path $cleanupFailureOutput `
    'DSPPluginManager.RM22CleanupFailure.dll'
$cleanupSuccessDll = Join-Path $cleanupSuccessOutput `
    'DSPPluginManager.RM22CleanupSuccess.dll'
$harmonyFailureDll = Join-Path $harmonyFailureOutput `
    'DSPPluginManager.RM23HarmonyActivationFailure.dll'
$harmonyLifecycleDll = Join-Path $harmonyLifecycleOutput `
    'DSPPluginManager.RM23HarmonyLifecycle.dll'
$staleTestCecil = Join-Path $testOutput 'Mono.Cecil.dll'
if (Test-Path -LiteralPath $staleTestCecil -PathType Leaf) {
    Remove-Item -LiteralPath $staleTestCecil -Force
}
foreach ($outputDirectory in @(
        $contractOutput,
        $consumerOutput,
        $constructionFailureOutput,
        $activationFailureOutput,
        $runtimeDeliveryOutput,
        $cleanupFailureOutput,
        $cleanupSuccessOutput,
        $harmonyFailureOutput,
        $harmonyLifecycleOutput,
        $handoffOutput
    )) {
    if (Test-Path -LiteralPath (
            Join-Path $outputDirectory 'UnityEngine.CoreModule.dll'
        )) {
        throw "Unity compile input leaked into build output: $outputDirectory"
    }
}
$reservedOutputNames = @(
    '0Harmony.dll',
    'MonoMod.RuntimeDetour.dll',
    'MonoMod.Utils.dll',
    'Mono.Cecil.dll'
)
foreach ($fixtureOutput in @(
        $harmonyFailureOutput,
        $harmonyLifecycleOutput
    )) {
    foreach ($reservedName in $reservedOutputNames) {
        if (Test-Path -LiteralPath (
                Join-Path $fixtureOutput $reservedName
            )) {
            throw "Manager-owned dependency leaked into RM-23 fixture output: $fixtureOutput\$reservedName"
        }
    }
}
$testExecutable = Join-Path $testOutput 'DSPPluginManager.Tests.exe'
& $testExecutable `
    $productDll `
    $AssemblyVersion `
    $ReleaseLabel `
    $dependencyRuntime `
    $unityHostDll `
    $facadeDll `
    $contractDll `
    $consumerDll `
    $constructionFailureDll `
    $activationFailureDll `
    $runtimeDeliveryDll `
    $cleanupFailureDll `
    $cleanupSuccessDll
if ($LASTEXITCODE -ne 0) {
    throw 'Compiled product tests failed.'
}
$staleHarmonyTestCecil = Join-Path $harmonyTestOutput 'Mono.Cecil.dll'
if (Test-Path -LiteralPath $staleHarmonyTestCecil -PathType Leaf) {
    Remove-Item -LiteralPath $staleHarmonyTestCecil -Force
}
$harmonyTestExecutable = Join-Path $harmonyTestOutput `
    'DSPPluginManager.HarmonyTests.exe'
& $harmonyTestExecutable `
    $dependencyRuntime `
    $unityHostDll `
    $facadeDll `
    $contractDll `
    $cleanupSuccessDll `
    $harmonyFailureDll `
    $harmonyLifecycleDll
if ($LASTEXITCODE -ne 0) {
    throw 'RM-23 isolated Harmony lifecycle tests failed.'
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
