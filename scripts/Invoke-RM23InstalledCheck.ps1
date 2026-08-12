[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,

    [string]$BundleRoot = '',

    [string]$NonHarmonyFixturePath = '',

    [string]$HarmonyFailureFixturePath = '',

    [string]$HarmonyLifecycleFixturePath = '',

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
if ([string]::IsNullOrWhiteSpace($NonHarmonyFixturePath)) {
    $NonHarmonyFixturePath = Join-Path $repositoryPath `
        'artifacts\fixtures\rm22-cleanup-success\DSPPluginManager.RM22CleanupSuccess.dll'
}
if ([string]::IsNullOrWhiteSpace($HarmonyFailureFixturePath)) {
    $HarmonyFailureFixturePath = Join-Path $repositoryPath `
        'artifacts\fixtures\rm23-harmony-activation-failure\DSPPluginManager.RM23HarmonyActivationFailure.dll'
}
if ([string]::IsNullOrWhiteSpace($HarmonyLifecycleFixturePath)) {
    $HarmonyLifecycleFixturePath = Join-Path $repositoryPath `
        'artifacts\fixtures\rm23-harmony-lifecycle\DSPPluginManager.RM23HarmonyLifecycle.dll'
}
$bundlePath = (Resolve-Path -LiteralPath $BundleRoot).Path
$nonHarmonyFixture = (
    Resolve-Path -LiteralPath $NonHarmonyFixturePath
).Path
$harmonyFailureFixture = (
    Resolve-Path -LiteralPath $HarmonyFailureFixturePath
).Path
$harmonyLifecycleFixture = (
    Resolve-Path -LiteralPath $HarmonyLifecycleFixturePath
).Path
$gameExecutable = Join-Path $gamePath 'DSPGAME.exe'
$configurationPath = Join-Path $gamePath 'doorstop_config.ini'
$proxyPath = Join-Path $gamePath 'winhttp.dll'
$managerPath = Join-Path $gamePath 'DSPPluginManager'
$expectedManagerPath = [IO.Path]::GetFullPath($managerPath)
$bundleProxy = Join-Path $bundlePath 'winhttp.dll'
$bundleManager = Join-Path $bundlePath 'DSPPluginManager'
$currentLogPath = Join-Path $managerPath 'logs\DSPPluginManager.log'
$checkpointPath = Join-Path $managerPath 'bootstrap-checkpoint.txt'
$nonHarmonyEvidencePath = Join-Path $managerPath `
    'writable\fixture.rm22.b-cleanup-success\RM22-SUCCESS-EVIDENCE.log'
$harmonyEvidencePath = Join-Path $managerPath `
    'writable\fixture.rm23.b-harmony-lifecycle\RM23-HARMONY-EVIDENCE.log'
$dependencyPath = Join-Path $managerPath 'dependencies'
$resultRoot = Join-Path $repositoryPath 'artifacts\rm23-installed-check'
$resultPath = Join-Path $resultRoot (
    (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssfffZ')
)
$configurationBackupPath = Join-Path $resultPath `
    'doorstop_config.original.ini'

foreach ($required in @(
        $gameExecutable,
        $SteamExecutable,
        $bundleProxy,
        (Join-Path $bundlePath 'doorstop_config.ini'),
        (Join-Path $bundleManager 'DSPPluginManager.dll'),
        $nonHarmonyFixture,
        $harmonyFailureFixture,
        $harmonyLifecycleFixture
    )) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required RM-23 input was not found: $required"
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
        throw "RM-23 evidence was not written: $Path"
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
        throw "RM-23 evidence expected $Name=$Expected."
    }
}

$configurationExisted = Test-Path -LiteralPath $configurationPath -PathType Leaf
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
    (Join-Path $gamePath 'DSPGAME_Data\Managed\Assembly-CSharp.dll'),
    (Join-Path $gamePath 'DSPGAME_Data\Managed\UnityEngine.CoreModule.dll')
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
    $pluginPath = Join-Path $managerPath 'plugins'
    New-Item -ItemType Directory -Force -Path $pluginPath | Out-Null
    foreach ($fixture in @(
            $nonHarmonyFixture,
            $harmonyFailureFixture,
            $harmonyLifecycleFixture
        )) {
        Copy-Item -LiteralPath $fixture -Destination $pluginPath
    }
    [IO.File]::WriteAllBytes(
        (Join-Path $managerPath 'bootstrap-checkpoint.enabled'),
        [byte[]]@()
    )
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
        throw 'RM-23 installed DSP did not complete its orderly exit.'
    }
    if (-not $respondingObserved) {
        throw 'RM-23 did not observe a responsive installed DSP window.'
    }

    $nonHarmonyEvidence = Read-KeyValues -Path $nonHarmonyEvidencePath
    Require-Value $nonHarmonyEvidence 'cleanupCount' '1'
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
    if ($harmonyEvidence.activationThread -cne
        $harmonyEvidence.cleanupThread -or
        $nonHarmonyEvidence.cleanupThread -cne
        $harmonyEvidence.cleanupThread) {
        throw 'RM-23 lifecycle callbacks did not remain on one Unity thread.'
    }

    $checkpoint = [IO.File]::ReadAllText($checkpointPath)
    $handoffLine = @($checkpoint -split '\r?\n' | Where-Object {
            $_ -match '\| event=unity-main-thread-handoff \|'
        })
    if ($handoffLine.Count -ne 1 -or
        $handoffLine[0] -notmatch '\| thread=(\d+) \|' -or
        $Matches[1] -cne $harmonyEvidence.cleanupThread) {
        throw 'RM-23 callbacks did not match the Unity handoff thread.'
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
            $expectedPath = Join-Path $dependencyPath "$assemblyName.dll"
            Require-Value $harmonyEvidence `
                "$phase.$assemblyName.path" $expectedPath
        }
    }

    $currentLog = [IO.File]::ReadAllText($currentLogPath)
    foreach ($message in @(
            'RM-23 intentional Harmony patch failure entered.',
            'Plugin activation failed: identifier=fixture.rm23.a-harmony-activation-failure version=1.0.0',
            'RM-23 attributable Harmony postfix applied: owner=fixture.rm23.b-harmony-lifecycle result=112.',
            'RM-22 success fixture cleanup entered.',
            'RM-23 Harmony cleanup verified: ownedRemoved=True otherOwnerPreserved=True resultAfterOwnedRemoval=102 finalResult=2.',
            'Plugin cleanup acknowledged: identifier=fixture.rm22.b-cleanup-success version=1.0.0 state=Stopped.',
            'Plugin cleanup acknowledged: identifier=fixture.rm23.b-harmony-lifecycle version=1.0.0 state=Stopped.',
            'Orderly plugin shutdown completed; closing current-run log.'
        )) {
        if ($currentLog.IndexOf($message, [StringComparison]::Ordinal) -lt 0) {
            throw "RM-23 current-run log is missing: $message"
        }
    }
    $exclusiveLog = New-Object IO.FileStream(
        $currentLogPath,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        [IO.FileShare]::None
    )
    $exclusiveLog.Dispose()

    foreach ($source in @(
            $nonHarmonyEvidencePath,
            $harmonyEvidencePath,
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
            "Unity main thread: $($harmonyEvidence.cleanupThread)",
            'Exact manager-owned Harmony closure during activation: True',
            'Exact manager-owned Harmony closure during cleanup: True',
            'Attributable postfix result: 112',
            'Owned patch removed while other owner remained: True',
            'Result after owned removal: 102',
            'Final unpatched result: 2',
            'Harmony activation failure recorded as Failed: True',
            'Non-Harmony lifecycle reached Stopped: True',
            'Harmony lifecycle reached Stopped: True',
            'Current-run log closed after lifecycle outcomes: True'
        ),
        (New-Object Text.UTF8Encoding($false))
    )
}
finally {
    Stop-InstalledGame
    foreach ($source in @(
            $nonHarmonyEvidencePath,
            $harmonyEvidencePath,
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
            throw "Protected game file changed during RM-23: $protectedFile"
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
        'DSP process stopped: True'
    ) -join [Environment]::NewLine) + [Environment]::NewLine),
    (New-Object Text.UTF8Encoding($false))
)
Write-Output "RM-23 installed check passed: $resultPath"
