[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,

    [string]$BundleRoot = '',

    [string]$FailureFixturePath = '',

    [string]$SuccessFixturePath = '',

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
if ([string]::IsNullOrWhiteSpace($FailureFixturePath)) {
    $FailureFixturePath = Join-Path $repositoryPath `
        'artifacts\fixtures\rm22-cleanup-failure\DSPPluginManager.RM22CleanupFailure.dll'
}
if ([string]::IsNullOrWhiteSpace($SuccessFixturePath)) {
    $SuccessFixturePath = Join-Path $repositoryPath `
        'artifacts\fixtures\rm22-cleanup-success\DSPPluginManager.RM22CleanupSuccess.dll'
}
$bundlePath = (Resolve-Path -LiteralPath $BundleRoot).Path
$failureFixture = (Resolve-Path -LiteralPath $FailureFixturePath).Path
$successFixture = (Resolve-Path -LiteralPath $SuccessFixturePath).Path
$gameExecutable = Join-Path $gamePath 'DSPGAME.exe'
$configurationPath = Join-Path $gamePath 'doorstop_config.ini'
$proxyPath = Join-Path $gamePath 'winhttp.dll'
$managerPath = Join-Path $gamePath 'DSPPluginManager'
$expectedManagerPath = [IO.Path]::GetFullPath($managerPath)
$bundleProxy = Join-Path $bundlePath 'winhttp.dll'
$bundleManager = Join-Path $bundlePath 'DSPPluginManager'
$checkpointPath = Join-Path $managerPath 'bootstrap-checkpoint.txt'
$currentLogPath = Join-Path $managerPath 'logs\DSPPluginManager.log'
$failureEvidencePath = Join-Path $managerPath `
    'writable\fixture.rm22.a-cleanup-failure\RM22-FAILURE-EVIDENCE.log'
$successEvidencePath = Join-Path $managerPath `
    'writable\fixture.rm22.b-cleanup-success\RM22-SUCCESS-EVIDENCE.log'
$resultRoot = Join-Path $repositoryPath 'artifacts\rm22-installed-check'
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
        $failureFixture,
        $successFixture
    )) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required RM-22 input was not found: $required"
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

function Read-Evidence {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "RM-22 evidence was not written: $Path"
    }
    $values = @{}
    foreach ($line in [IO.File]::ReadAllLines($Path)) {
        if ($line -match '^([^=]+)=(.*)$') {
            $values[$Matches[1]] = $Matches[2]
        }
    }
    foreach ($name in @(
            'cleanupCount',
            'cleanupThread',
            'loggerAvailable',
            'writableRootAvailable',
            'componentAvailable',
            'contractAvailable',
            'unityAvailable'
        )) {
        if (-not $values.ContainsKey($name)) {
            throw "RM-22 evidence is missing '$name': $Path"
        }
    }
    if ($values.cleanupCount -cne '1') {
        throw "RM-22 cleanup callback count was not exactly one: $Path"
    }
    foreach ($name in @(
            'loggerAvailable',
            'writableRootAvailable',
            'componentAvailable',
            'contractAvailable',
            'unityAvailable'
        )) {
        if ($values[$name] -cne 'True') {
            throw "RM-22 evidence expected $name=True: $Path"
        }
    }
    return $values
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
$recoveryLines = @(
    "Game root: $gamePath",
    "Temporary manager path: $managerPath",
    "Doorstop configuration existed: $configurationExisted",
    "Doorstop configuration backup: $configurationBackupPath",
    "Doorstop configuration SHA-256: $configurationHash",
    "Proxy existed before check: $proxyExisted"
)
[IO.File]::WriteAllLines(
    (Join-Path $resultPath 'RECOVERY.txt'),
    $recoveryLines,
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
    Copy-Item -LiteralPath $failureFixture -Destination $pluginPath
    Copy-Item -LiteralPath $successFixture -Destination $pluginPath
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
        throw 'RM-22 installed DSP did not complete its orderly exit.'
    }
    if (-not $respondingObserved) {
        throw 'RM-22 did not observe a responsive installed DSP window.'
    }

    $failureEvidence = Read-Evidence -Path $failureEvidencePath
    $successEvidence = Read-Evidence -Path $successEvidencePath
    if ($failureEvidence.cleanupThread -cne $successEvidence.cleanupThread -or
        $successEvidence.activateThread -cne $successEvidence.cleanupThread) {
        throw 'RM-22 cleanup did not remain on the established Unity thread.'
    }
    $checkpoint = [IO.File]::ReadAllText($checkpointPath)
    $handoffLine = @($checkpoint -split '\r?\n' | Where-Object {
            $_ -match '\| event=unity-main-thread-handoff \|'
        })
    if ($handoffLine.Count -ne 1 -or
        $handoffLine[0] -notmatch '\| thread=(\d+) \|' -or
        $Matches[1] -cne $successEvidence.cleanupThread) {
        throw 'RM-22 cleanup did not match the Unity handoff thread.'
    }

    $currentLog = [IO.File]::ReadAllText($currentLogPath)
    $orderedMessages = @(
        'RM-22 failure fixture cleanup entered.',
        'RM-22 success fixture cleanup entered.',
        'Plugin cleanup failed: identifier=fixture.rm22.a-cleanup-failure version=1.0.0 state=StopFailed phase=deactivation.',
        'Plugin cleanup acknowledged: identifier=fixture.rm22.b-cleanup-success version=1.0.0 state=Stopped.',
        'Orderly plugin shutdown completed; closing current-run log.'
    )
    $priorIndex = -1
    foreach ($message in $orderedMessages) {
        $index = $currentLog.IndexOf($message, [StringComparison]::Ordinal)
        if ($index -le $priorIndex) {
            throw "RM-22 current-run log ordering failed at: $message"
        }
        $priorIndex = $index
    }
    if ($currentLog.IndexOf(
            'System.InvalidOperationException: RM-22 intentional cleanup failure.',
            [StringComparison]::Ordinal
        ) -lt 0) {
        throw 'RM-22 current-run log did not retain the full cleanup exception.'
    }
    $exclusiveLog = New-Object IO.FileStream(
        $currentLogPath,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        [IO.FileShare]::None
    )
    $exclusiveLog.Dispose()

    Copy-Item -LiteralPath $failureEvidencePath -Destination $resultPath
    Copy-Item -LiteralPath $successEvidencePath -Destination $resultPath
    Copy-Item -LiteralPath $currentLogPath -Destination $resultPath
    Copy-Item -LiteralPath $checkpointPath -Destination $resultPath
    $resultLines = @(
        "Installed executable: $gameExecutable",
        "Process: $($process.Id)",
        "Responsive window before orderly exit: $respondingObserved",
        "Unity main thread: $($successEvidence.cleanupThread)",
        'Failure cleanup callback count: 1',
        'Success cleanup callback count: 1',
        'Services and component usable through both callbacks: True',
        'Failure terminal state: StopFailed',
        'Success terminal state: Stopped',
        'Later cleanup continued after failure: True',
        'Complete attributable cleanup exception retained: True',
        'Current-run log ordered, flushed, and exclusively reopenable: True'
    )
    [IO.File]::WriteAllLines(
        (Join-Path $resultPath 'RUN-RESULT.txt'),
        $resultLines,
        (New-Object Text.UTF8Encoding($false))
    )
}
finally {
    Stop-InstalledGame
    foreach ($source in @(
            $failureEvidencePath,
            $successEvidencePath,
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
            throw "Protected game file changed during RM-22: $protectedFile"
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
Write-Output "RM-22 installed check passed: $resultPath"
