[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,

    [string]$BundleRoot = '',

    [string]$SteamExecutable =
        'C:\Program Files (x86)\Steam\steam.exe',

    [ValidatePattern('^\d+$')]
    [string]$SteamAppId = '1366540',

    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [ValidateRange(30, 180)]
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
$repositoryPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$gamePath = (Resolve-Path -LiteralPath $GameRoot).Path
if ([string]::IsNullOrWhiteSpace($BundleRoot)) {
    $BundleRoot = Join-Path $repositoryPath 'artifacts\bootstrap-bundle'
}
$bundlePath = (Resolve-Path -LiteralPath $BundleRoot).Path
$bundleManager = Join-Path $bundlePath 'DSPPluginManager'
$bundleProxy = Join-Path $bundlePath 'winhttp.dll'
$gameExecutable = Join-Path $gamePath 'DSPGAME.exe'
$configurationPath = Join-Path $gamePath 'doorstop_config.ini'
$proxyPath = Join-Path $gamePath 'winhttp.dll'
$managerPath = Join-Path $gamePath 'DSPPluginManager'
$expectedManagerPath = [IO.Path]::GetFullPath($managerPath)
$pluginPath = Join-Path $managerPath 'plugins'
$dependencyPath = Join-Path $managerPath 'dependencies'
$contractPath = Join-Path $managerPath 'DSPPluginManager.Contracts.dll'
$currentLogPath = Join-Path $managerPath 'logs\DSPPluginManager.log'
$checkpointPath = Join-Path $managerPath 'bootstrap-checkpoint.txt'
$candidateSentinelPath = Join-Path $managerPath `
    'candidate-code-executed.txt'
$runtimeEvidencePath = Join-Path $managerPath `
    'writable\fixture.rm21.runtime-delivery\RM21-RUNTIME-EVIDENCE.log'
$cleanupFailureEvidencePath = Join-Path $managerPath `
    'writable\fixture.rm22.a-cleanup-failure\RM22-FAILURE-EVIDENCE.log'
$cleanupSuccessEvidencePath = Join-Path $managerPath `
    'writable\fixture.rm22.b-cleanup-success\RM22-SUCCESS-EVIDENCE.log'
$harmonyEvidencePath = Join-Path $managerPath `
    'writable\fixture.rm23.b-harmony-lifecycle\RM23-HARMONY-EVIDENCE.log'
$contractTestExecutable = Join-Path $repositoryPath `
    'artifacts\contract-tests\DSPPluginManager.ContractTests.exe'
$consumerFixture = Join-Path $repositoryPath `
    'artifacts\fixtures\rm09-consumer\DSPPluginManager.RM09Consumer.dll'
$lifecycleFixtures = @(
    (Join-Path $repositoryPath `
        'artifacts\fixtures\rm21-runtime-delivery\DSPPluginManager.RM21RuntimeDelivery.dll'),
    (Join-Path $repositoryPath `
        'artifacts\fixtures\rm22-cleanup-failure\DSPPluginManager.RM22CleanupFailure.dll'),
    (Join-Path $repositoryPath `
        'artifacts\fixtures\rm22-cleanup-success\DSPPluginManager.RM22CleanupSuccess.dll'),
    (Join-Path $repositoryPath `
        'artifacts\fixtures\rm23-harmony-activation-failure\DSPPluginManager.RM23HarmonyActivationFailure.dll'),
    (Join-Path $repositoryPath `
        'artifacts\fixtures\rm23-harmony-lifecycle\DSPPluginManager.RM23HarmonyLifecycle.dll')
)
$managedPath = Join-Path $gamePath 'DSPGAME_Data\Managed'
$resultRoot = Join-Path $repositoryPath `
    'artifacts\milestone2-installed-check'
$resultPath = Join-Path $resultRoot (
    (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssfffZ')
)
$expectedPlanPath = Join-Path $resultPath 'EXPECTED-PLAN.txt'
$configurationBackupPath = Join-Path $resultPath `
    'doorstop_config.original.ini'

foreach ($required in @(
        $gameExecutable,
        $SteamExecutable,
        $bundleProxy,
        (Join-Path $bundlePath 'doorstop_config.ini'),
        (Join-Path $bundleManager 'DSPPluginManager.dll'),
        $contractTestExecutable,
        $consumerFixture
    ) + $lifecycleFixtures) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required Milestone 2 input was not found: $required"
    }
}
if (Test-Path -LiteralPath $managerPath) {
    throw "Manager install path already exists: $managerPath"
}
if (@(Get-Process -Name 'DSPGAME' -ErrorAction SilentlyContinue).Count -ne 0) {
    throw 'A DSPGAME process is already running; refusing to alter the installed bootstrap configuration.'
}

function Stop-InstalledGame {
    $processes = @(Get-Process -Name 'DSPGAME' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -ceq $gameExecutable })
    foreach ($running in $processes) {
        $null = $running.CloseMainWindow()
        if (-not $running.WaitForExit(5000)) {
            Stop-Process -Id $running.Id -Force
            $running.WaitForExit()
        }
        $running.Dispose()
    }
}

function Read-KeyValues {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Milestone 2 evidence was not written: $Path"
    }
    $values = @{}
    foreach ($line in [IO.File]::ReadAllLines($Path)) {
        if ($line -match '^([^=]+)=(.*)$') {
            $values[$Matches[1]] = $Matches[2]
        }
    }
    return $values
}

function Require-Value {
    param(
        [Parameter(Mandatory = $true)][hashtable]$Values,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Expected
    )
    if (-not $Values.ContainsKey($Name) -or
        $Values[$Name] -cne $Expected) {
        throw "Milestone 2 evidence expected $Name=$Expected."
    }
}

$configurationExisted = Test-Path -LiteralPath $configurationPath `
    -PathType Leaf
$configurationHash = if ($configurationExisted) {
    (Get-FileHash -LiteralPath $configurationPath -Algorithm SHA256).Hash
}
$proxyExisted = Test-Path -LiteralPath $proxyPath -PathType Leaf
$proxyHash = if ($proxyExisted) {
    (Get-FileHash -LiteralPath $proxyPath -Algorithm SHA256).Hash
}
$bundleProxyHash = (
    Get-FileHash -LiteralPath $bundleProxy -Algorithm SHA256
).Hash
if ($proxyExisted -and $proxyHash -cne $bundleProxyHash) {
    throw 'An existing winhttp.dll differs from the pinned manager proxy; refusing the collision.'
}
$protectedFiles = @(
    $gameExecutable,
    (Join-Path $managedPath 'Assembly-CSharp.dll'),
    (Join-Path $managedPath 'UnityEngine.CoreModule.dll')
)
$beforeHashes = @{}
foreach ($protectedFile in $protectedFiles) {
    $beforeHashes[$protectedFile] = (
        Get-FileHash -LiteralPath $protectedFile -Algorithm SHA256
    ).Hash
}
$existingEmergencyFiles = @(
    Get-ChildItem -LiteralPath $gamePath -File `
        -Filter 'DSPPluginManager-bootstrap-failure-*.txt' |
        ForEach-Object FullName
)
$createdProxy = $false
$launcher = $null
$process = $null
$respondingObserved = $false

New-Item -ItemType Directory -Force -Path $resultPath | Out-Null
if ($configurationExisted) {
    Copy-Item -LiteralPath $configurationPath `
        -Destination $configurationBackupPath
}
[IO.File]::WriteAllLines(
    (Join-Path $resultPath 'RECOVERY.txt'),
    @(
        "Game root: $gamePath",
        "Temporary manager path: $managerPath",
        "Doorstop configuration existed: $configurationExisted",
        "Doorstop configuration backup: $configurationBackupPath",
        "Doorstop configuration SHA-256: $configurationHash",
        "Proxy existed before check: $proxyExisted"
    ),
    (New-Object Text.UTF8Encoding($false))
)

try {
    if (-not $proxyExisted) {
        Copy-Item -LiteralPath $bundleProxy -Destination $proxyPath
        $createdProxy = $true
    }
    Copy-Item -LiteralPath $bundleManager -Destination $managerPath -Recurse
    [IO.File]::WriteAllBytes(
        (Join-Path $managerPath 'bootstrap-checkpoint.enabled'),
        [byte[]]@()
    )

    & $contractTestExecutable `
        '--write-milestone2-fixture' `
        $contractPath `
        $consumerFixture `
        $dependencyPath `
        $managedPath `
        $pluginPath `
        $candidateSentinelPath `
        $lifecycleFixtures[0] `
        $lifecycleFixtures[1] `
        $lifecycleFixtures[2] `
        $lifecycleFixtures[3] `
        $lifecycleFixtures[4] `
        $expectedPlanPath
    if ($LASTEXITCODE -ne 0) {
        throw 'The deterministic Milestone 2 fixture could not be generated.'
    }

    Copy-Item -LiteralPath (
        Join-Path $bundlePath 'doorstop_config.ini'
    ) -Destination $configurationPath -Force
    $launchStarted = [DateTime]::UtcNow
    $launcher = Start-Process -FilePath $SteamExecutable `
        -ArgumentList '-applaunch', $SteamAppId `
        -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 500
        if ($null -eq $process) {
            $process = Get-Process -Name 'DSPGAME' `
                -ErrorAction SilentlyContinue | Where-Object {
                    $_.Path -ceq $gameExecutable -and
                    $_.StartTime.ToUniversalTime() -ge $launchStarted
                } | Sort-Object StartTime -Descending |
                Select-Object -First 1
            if ($null -eq $process) {
                continue
            }
        }
        $process.Refresh()
        if ($process.MainWindowHandle -ne [IntPtr]::Zero -and
            $process.Responding) {
            $respondingObserved = $true
        }
        if ($process.HasExited) {
            break
        }
    }
    if ($null -eq $process -or -not $process.HasExited) {
        throw 'Milestone 2 installed DSP did not complete its orderly exit.'
    }
    if (-not $respondingObserved) {
        throw 'Milestone 2 did not observe a responsive installed DSP window.'
    }

    $runtimeEvidence = Read-KeyValues -Path $runtimeEvidencePath
    foreach ($expected in @{
            resumeHandleUsable = 'True'
            cancelHandleUsable = 'True'
            handlesDistinct = 'True'
            cancelledStarted = 'True'
            cancelledResumed = 'False'
            resumedAfterNull = 'True'
            probeSceneActivated = 'True'
            originalSceneRestored = 'True'
            sceneTransitionComplete = 'True'
        }.GetEnumerator()) {
        Require-Value $runtimeEvidence $expected.Key $expected.Value
    }
    if ([int]$runtimeEvidence.awakeCount -ne 1 -or
        [int]$runtimeEvidence.updateCount -lt 4 -or
        [int]$runtimeEvidence.awakeSequence -ge
            [int]$runtimeEvidence.firstUpdateSequence -or
        [int]$runtimeEvidence.resumeFrame -le
            [int]$runtimeEvidence.resumeStartFrame) {
        throw 'Milestone 2 Unity frame or coroutine evidence was invalid.'
    }
    if ($runtimeEvidence.awakeInstanceId -cne
            $runtimeEvidence.updateInstanceId -or
        $runtimeEvidence.rootBeforeId -cne $runtimeEvidence.rootDuringId -or
        $runtimeEvidence.rootBeforeId -cne $runtimeEvidence.rootAfterId) {
        throw 'Milestone 2 component or host persistence evidence was invalid.'
    }

    $cleanupFailure = Read-KeyValues -Path $cleanupFailureEvidencePath
    $cleanupSuccess = Read-KeyValues -Path $cleanupSuccessEvidencePath
    foreach ($evidence in @($cleanupFailure, $cleanupSuccess)) {
        foreach ($name in @(
                'loggerAvailable',
                'writableRootAvailable',
                'componentAvailable',
                'contractAvailable',
                'unityAvailable'
            )) {
            Require-Value $evidence $name 'True'
        }
        Require-Value $evidence 'cleanupCount' '1'
    }

    $harmonyEvidence = Read-KeyValues -Path $harmonyEvidencePath
    foreach ($expected in @{
            activationCount = '1'
            cleanupCount = '1'
            patchedResult = '112'
            ownedRemovalResult = '102'
            finalResult = '2'
            ownedPatchAttributed = 'True'
            controlPatchAttributed = 'True'
            ownedPatchRemoved = 'True'
            controlPatchPreserved = 'True'
            allPatchesRemoved = 'True'
        }.GetEnumerator()) {
        Require-Value $harmonyEvidence $expected.Key $expected.Value
    }

    $threadValues = @(
        $runtimeEvidence.awakeThread,
        $runtimeEvidence.activateThread,
        $runtimeEvidence.firstUpdateThread,
        $runtimeEvidence.resumeThread,
        $runtimeEvidence.sceneThread,
        $cleanupFailure.cleanupThread,
        $cleanupSuccess.activateThread,
        $cleanupSuccess.cleanupThread,
        $harmonyEvidence.activationThread,
        $harmonyEvidence.cleanupThread
    ) | Sort-Object -Unique
    if ($threadValues.Count -ne 1) {
        throw 'Milestone 2 callbacks did not remain on one Unity thread.'
    }

    $checkpoint = [IO.File]::ReadAllText($checkpointPath)
    $handoffLines = @($checkpoint -split '\r?\n' | Where-Object {
            $_ -match '\| event=unity-main-thread-handoff \|'
        })
    if ($handoffLines.Count -ne 1 -or
        $handoffLines[0] -notmatch '\| thread=(\d+) \|' -or
        $Matches[1] -cne $threadValues[0]) {
        throw 'Milestone 2 callbacks did not match the Unity handoff thread.'
    }

    $versions = @{
        '0Harmony' = '2.5.5.0'
        'MonoMod.RuntimeDetour' = '21.9.19.1'
        'MonoMod.Utils' = '21.9.19.1'
        'Mono.Cecil' = '0.10.4.0'
    }
    foreach ($phase in @('activation', 'cleanup')) {
        foreach ($assemblyName in $versions.Keys) {
            Require-Value $harmonyEvidence `
                "$phase.$assemblyName.version" $versions[$assemblyName]
            Require-Value $harmonyEvidence `
                "$phase.$assemblyName.path" (
                    Join-Path $dependencyPath "$assemblyName.dll"
                )
        }
    }

    $currentLog = [IO.File]::ReadAllText($currentLogPath)
    if (-not $currentLog.Contains('runtimeLoadedCandidates=0.')) {
        throw 'Milestone 2 discovery did not prove zero early runtime loads.'
    }
    $expectedPlan = [IO.File]::ReadAllLines($expectedPlanPath)
    $actualPlan = @([Regex]::Matches(
        $currentLog,
        'DiscoveryPlan\|(?<plan>state=[^\r\n]+)'
    ) | ForEach-Object { $_.Groups['plan'].Value })
    if ($actualPlan.Count -ne $expectedPlan.Count -or
        (Compare-Object -ReferenceObject $expectedPlan `
            -DifferenceObject $actualPlan -SyncWindow 0).Count -ne 0) {
        throw 'The installed candidate plan differed from the expected plan.'
    }
    if (-not (Test-Path -LiteralPath $candidateSentinelPath -PathType Leaf)) {
        throw 'The selected Milestone 1 fixture was not runtime-executed.'
    }
    $activationLines = @($currentLog -split '\r?\n' | Where-Object {
            $_ -match 'Plugin activation (acknowledged|failed): identifier='
        })
    if ($activationLines.Count -ne 6 -or
        @($activationLines | Where-Object {
                $_ -match 'identifier=com\.example\.ambiguous'
            }).Count -ne 0) {
        throw 'Milestone 2 did not retain exactly the six selected activation outcomes.'
    }

    foreach ($message in @(
            'Plugin activation acknowledged: identifier=com.shytamir.dspmirrorblueprint version=1.2.3',
            'Plugin activation acknowledged: identifier=fixture.rm21.runtime-delivery version=1.0.0',
            'RM-21 ordinary Unity runtime delivery evidence completed.',
            'Plugin activation failed: identifier=fixture.rm23.a-harmony-activation-failure version=1.0.0',
            'RM-23 attributable Harmony postfix applied: owner=fixture.rm23.b-harmony-lifecycle result=112.',
            'Plugin cleanup failed: identifier=fixture.rm22.a-cleanup-failure version=1.0.0 state=StopFailed phase=deactivation.',
            'Plugin cleanup acknowledged: identifier=fixture.rm22.b-cleanup-success version=1.0.0 state=Stopped.',
            'Plugin cleanup acknowledged: identifier=fixture.rm23.b-harmony-lifecycle version=1.0.0 state=Stopped.',
            'Orderly plugin shutdown completed; closing current-run log.'
        )) {
        if ($currentLog.IndexOf($message, [StringComparison]::Ordinal) -lt 0) {
            throw "Milestone 2 current-run log is missing: $message"
        }
    }
    if ($currentLog.IndexOf(
            'System.InvalidOperationException: RM-22 intentional cleanup failure.',
            [StringComparison]::Ordinal
        ) -lt 0) {
        throw 'Milestone 2 log did not retain the cleanup exception.'
    }
    $exclusiveLog = New-Object IO.FileStream(
        $currentLogPath,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        [IO.FileShare]::None
    )
    $exclusiveLog.Dispose()

    foreach ($source in @(
            $runtimeEvidencePath,
            $cleanupFailureEvidencePath,
            $cleanupSuccessEvidencePath,
            $harmonyEvidencePath,
            $candidateSentinelPath,
            $currentLogPath,
            $checkpointPath
        )) {
        Copy-Item -LiteralPath $source -Destination $resultPath
    }
    [IO.File]::WriteAllLines(
        (Join-Path $resultPath 'RUN-RESULT.txt'),
        @(
            "Installed executable: $gameExecutable",
            "Process: $($process.Id)",
            "Responsive window before orderly exit: $respondingObserved",
            "Unity main thread: $($threadValues[0])",
            "Expected and installed plan entries: $($expectedPlan.Count)",
            'Selected candidates: 6',
            'Early runtime-loaded candidate count: 0',
            'Selected execution sentinel observed after activation: True',
            'Rejected, redundant, superseded, and ambiguous activation observed: False',
            'Independent active components: 5',
            'Isolated activation failure: 1',
            'Unity frame, coroutine, and scene persistence evidence: True',
            'Logger and writable-root services survived through cleanup: True',
            'Cleanup terminal outcomes: Stopped=4; StopFailed=1',
            'Attributable Harmony postfix result: 112',
            'Owned Harmony patch removed while other owner remained: True',
            'Harmony target final result: 2',
            'Current-run log closed after lifecycle outcomes: True',
            "Manager SHA-256: $((Get-FileHash -LiteralPath (Join-Path $managerPath 'DSPPluginManager.dll') -Algorithm SHA256).Hash)",
            "Contracts SHA-256: $((Get-FileHash -LiteralPath $contractPath -Algorithm SHA256).Hash)",
            "Unity host SHA-256: $((Get-FileHash -LiteralPath (Join-Path $managerPath 'DSPPluginManager.UnityHost.dll') -Algorithm SHA256).Hash)",
            "Proxy SHA-256: $bundleProxyHash"
        ),
        (New-Object Text.UTF8Encoding($false))
    )
}
finally {
    Stop-InstalledGame
    foreach ($source in @(
            $runtimeEvidencePath,
            $cleanupFailureEvidencePath,
            $cleanupSuccessEvidencePath,
            $harmonyEvidencePath,
            $candidateSentinelPath,
            $currentLogPath,
            $checkpointPath
        )) {
        if ((Test-Path -LiteralPath $source -PathType Leaf) -and
            -not (Test-Path -LiteralPath (
                Join-Path $resultPath (Split-Path -Leaf $source)
            ))) {
            Copy-Item -LiteralPath $source -Destination $resultPath
        }
    }
    if ($configurationExisted) {
        Copy-Item -LiteralPath $configurationBackupPath `
            -Destination $configurationPath -Force
        if ((Get-FileHash -LiteralPath $configurationPath `
                -Algorithm SHA256).Hash -cne $configurationHash) {
            throw 'The original Doorstop configuration was not restored exactly.'
        }
    }
    elseif (Test-Path -LiteralPath $configurationPath) {
        Remove-Item -LiteralPath $configurationPath -Force
    }
    if (Test-Path -LiteralPath $managerPath) {
        $resolvedManager = (Resolve-Path -LiteralPath $managerPath).Path
        if ($resolvedManager -cne $expectedManagerPath -or
            (Split-Path -Parent $resolvedManager) -cne $gamePath) {
            throw "Refusing to remove unexpected manager path: $resolvedManager"
        }
        Remove-Item -LiteralPath $resolvedManager -Recurse -Force
    }
    if ($createdProxy -and (Test-Path -LiteralPath $proxyPath)) {
        Remove-Item -LiteralPath $proxyPath -Force
    }
    if ($null -ne $launcher) {
        $launcher.Dispose()
    }
    if ($null -ne $process) {
        $process.Dispose()
    }
    foreach ($protectedFile in $protectedFiles) {
        if ((Get-FileHash -LiteralPath $protectedFile `
                -Algorithm SHA256).Hash -cne $beforeHashes[$protectedFile]) {
            throw "Protected game file changed during Milestone 2: $protectedFile"
        }
    }
    $newEmergencyFiles = @(
        Get-ChildItem -LiteralPath $gamePath -File `
            -Filter 'DSPPluginManager-bootstrap-failure-*.txt' |
            Where-Object { $existingEmergencyFiles -cnotcontains $_.FullName }
    )
    foreach ($emergencyFile in $newEmergencyFiles) {
        Copy-Item -LiteralPath $emergencyFile.FullName -Destination $resultPath
        Remove-Item -LiteralPath $emergencyFile.FullName -Force
    }
}

[IO.File]::AppendAllText(
    (Join-Path $resultPath 'RUN-RESULT.txt'),
    ((@(
        'Manager install path removed: True',
        'Original Doorstop configuration restored: True',
        'Protected game and Unity assemblies unchanged: True',
        "Pre-existing identical proxy retained: $proxyExisted",
        'DSP process stopped: True'
    ) -join [Environment]::NewLine) + [Environment]::NewLine),
    (New-Object Text.UTF8Encoding($false))
)
Write-Output "Milestone 2 installed check passed: $resultPath"
