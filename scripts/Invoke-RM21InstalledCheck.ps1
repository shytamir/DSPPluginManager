[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,

    [string]$BundleRoot = '',

    [string]$FixturePath = '',

    [string]$SteamExecutable = 'C:\Program Files (x86)\Steam\steam.exe',

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
if ([string]::IsNullOrWhiteSpace($FixturePath)) {
    $FixturePath = Join-Path $repositoryPath `
        'artifacts\fixtures\rm21-runtime-delivery\DSPPluginManager.RM21RuntimeDelivery.dll'
}
$bundlePath = (Resolve-Path -LiteralPath $BundleRoot).Path
$fixture = (Resolve-Path -LiteralPath $FixturePath).Path
$gameExecutable = Join-Path $gamePath 'DSPGAME.exe'
$configurationPath = Join-Path $gamePath 'doorstop_config.ini'
$proxyPath = Join-Path $gamePath 'winhttp.dll'
$managerPath = Join-Path $gamePath 'DSPPluginManager'
$expectedManagerPath = [IO.Path]::GetFullPath($managerPath)
$bundleProxy = Join-Path $bundlePath 'winhttp.dll'
$bundleManager = Join-Path $bundlePath 'DSPPluginManager'
$checkpointPath = Join-Path $managerPath 'bootstrap-checkpoint.txt'
$currentLogPath = Join-Path $managerPath 'logs\DSPPluginManager.log'
$evidencePath = Join-Path $managerPath `
    'writable\fixture.rm21.runtime-delivery\RM21-RUNTIME-EVIDENCE.log'
$resultRoot = Join-Path $repositoryPath 'artifacts\rm21-installed-check'
$resultPath = Join-Path $resultRoot (
    (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssfffZ')
)
$configurationBackupPath = Join-Path $resultPath `
    'doorstop_config.original.ini'
$recoveryPath = Join-Path $resultPath 'RECOVERY.txt'

foreach ($required in @(
        $gameExecutable,
        $SteamExecutable,
        $bundleProxy,
        (Join-Path $bundlePath 'doorstop_config.ini'),
        (Join-Path $bundleManager 'DSPPluginManager.dll'),
        $fixture
    )) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required RM-21 input was not found: $required"
    }
}
if (Test-Path -LiteralPath $managerPath) {
    throw "Manager install path already exists: $managerPath"
}
if (@(Get-Process -Name 'DSPGAME' -ErrorAction SilentlyContinue).Count -ne 0) {
    throw 'A DSPGAME process is already running; refusing to alter the installed bootstrap configuration.'
}

function Read-LiveUtf8File {
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = New-Object IO.FileStream(
        $Path,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        [IO.FileShare]::ReadWrite
    )
    try {
        $reader = New-Object IO.StreamReader(
            $stream,
            (New-Object Text.UTF8Encoding($false, $true)),
            $true
        )
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
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
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    $quietIntervals = 0
    while ([DateTime]::UtcNow -lt $deadline -and $quietIntervals -lt 4) {
        $remaining = @(Get-Process -Name 'DSPGAME' `
            -ErrorAction SilentlyContinue | Where-Object {
                $_.Path -ceq $gameExecutable
            })
        if ($remaining.Count -eq 0) {
            $quietIntervals++
        }
        else {
            $quietIntervals = 0
        }
        Start-Sleep -Seconds 1
    }
    if ($quietIntervals -lt 4) {
        throw 'The RM-21 installed DSP process did not remain stopped.'
    }
}

function Require-Value {
    param(
        [Parameter(Mandatory = $true)][hashtable]$Values,
        [Parameter(Mandatory = $true)][string]$Name
    )
    if (-not $Values.ContainsKey($Name)) {
        throw "RM-21 evidence is missing '$Name'."
    }
    return $Values[$Name]
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
$evidence = $null
$currentLog = $null
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
    "Proxy existed before check: $proxyExisted",
    "Proxy created by check: $(-not $proxyExisted)"
)
[IO.File]::WriteAllLines(
    $recoveryPath,
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
    Copy-Item -LiteralPath $fixture -Destination $pluginPath
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
        if ($process.HasExited) {
            throw 'DSP exited before the RM-21 runtime check completed.'
        }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero -and
            $process.Responding) {
            $respondingObserved = $true
        }
        if (Test-Path -LiteralPath $evidencePath -PathType Leaf) {
            try {
                $evidence = Read-LiveUtf8File -Path $evidencePath
            }
            catch [IO.IOException] {
                $evidence = $null
            }
        }
        if (Test-Path -LiteralPath $currentLogPath -PathType Leaf) {
            try {
                $currentLog = Read-LiveUtf8File -Path $currentLogPath
            }
            catch [IO.IOException] {
                $currentLog = $null
            }
        }
        if ($respondingObserved -and $null -ne $evidence -and
            $evidence.Contains('event=probe-complete') -and
            $null -ne $currentLog -and
            $currentLog.Contains(
                'RM-21 ordinary Unity runtime delivery evidence completed.'
            )) {
            break
        }
    }

    if (-not $respondingObserved) {
        throw 'RM-21 did not reach a responsive installed DSP window.'
    }
    if ($null -eq $evidence -or
        -not $evidence.Contains('event=probe-complete')) {
        throw 'RM-21 runtime-delivery evidence did not complete.'
    }
    if ($null -eq $currentLog -or
        -not $currentLog.Contains(
            'Plugin activation acknowledged: identifier=fixture.rm21.runtime-delivery'
        ) -or
        -not $currentLog.Contains(
            'RM-21 ordinary Unity runtime delivery evidence completed.'
        )) {
        throw 'RM-21 current-run log did not retain activation and completion.'
    }

    $values = @{}
    foreach ($line in @($evidence -split '\r?\n')) {
        if ($line -match '^([^=]+)=(.*)$' -and
            $Matches[1] -cne 'event') {
            $values[$Matches[1]] = $Matches[2]
        }
    }
    foreach ($booleanName in @(
            'resumeHandleUsable',
            'cancelHandleUsable',
            'handlesDistinct',
            'cancelledStarted',
            'resumedAfterNull',
            'probeSceneActivated',
            'originalSceneRestored',
            'sceneTransitionComplete'
        )) {
        if ((Require-Value -Values $values -Name $booleanName) -cne 'True') {
            throw "RM-21 evidence expected $booleanName=True."
        }
    }
    if ((Require-Value -Values $values -Name 'cancelledResumed') -cne 'False') {
        throw 'RM-21 stopped coroutine resumed unexpectedly.'
    }

    $awakeCount = [int](Require-Value -Values $values -Name 'awakeCount')
    $updateCount = [int](Require-Value -Values $values -Name 'updateCount')
    $awakeSequence = [int](
        Require-Value -Values $values -Name 'awakeSequence'
    )
    $updateSequence = [int](
        Require-Value -Values $values -Name 'firstUpdateSequence'
    )
    $resumeStartFrame = [int](
        Require-Value -Values $values -Name 'resumeStartFrame'
    )
    $resumeFrame = [int](Require-Value -Values $values -Name 'resumeFrame')
    if ($awakeCount -ne 1 -or $updateCount -lt 4 -or
        $awakeSequence -ge $updateSequence) {
        throw 'RM-21 Awake/Update count or ordering was invalid.'
    }
    if ($resumeFrame -le $resumeStartFrame) {
        throw 'RM-21 yield return null did not resume on a later frame.'
    }

    $threadValues = @(
        'awakeThread',
        'activateThread',
        'firstUpdateThread',
        'resumeThread',
        'sceneThread'
    ) | ForEach-Object {
        Require-Value -Values $values -Name $_
    } | Sort-Object -Unique
    if ($threadValues.Count -ne 1) {
        throw 'RM-21 Unity delivery left the established main thread.'
    }
    $checkpoint = [IO.File]::ReadAllText($checkpointPath)
    $handoffLine = @($checkpoint -split '\r?\n' | Where-Object {
            $_ -match '\| event=unity-main-thread-handoff \|'
        })
    if ($handoffLine.Count -ne 1 -or
        $handoffLine[0] -notmatch '\| thread=(\d+) \|' -or
        $Matches[1] -cne $threadValues[0]) {
        throw 'RM-21 fixture callbacks did not match the Unity handoff thread.'
    }

    $awakeInstance = Require-Value -Values $values -Name 'awakeInstanceId'
    $updateInstance = Require-Value -Values $values -Name 'updateInstanceId'
    $rootBefore = Require-Value -Values $values -Name 'rootBeforeId'
    $rootDuring = Require-Value -Values $values -Name 'rootDuringId'
    $rootAfter = Require-Value -Values $values -Name 'rootAfterId'
    if ($awakeInstance -cne $updateInstance -or
        $rootBefore -cne $rootDuring -or $rootBefore -cne $rootAfter) {
        throw 'RM-21 component or host-root identity changed across the scene transition.'
    }

    Copy-Item -LiteralPath $evidencePath -Destination $resultPath
    Copy-Item -LiteralPath $currentLogPath -Destination $resultPath
    Copy-Item -LiteralPath $checkpointPath -Destination $resultPath
    $resultLines = @(
        "Installed executable: $gameExecutable",
        "Process: $($process.Id)",
        "Responsive window: $respondingObserved",
        "Unity main thread: $($threadValues[0])",
        "Awake count: $awakeCount",
        "Rendered Update count before evidence: $updateCount",
        "Awake before first Update: $($awakeSequence -lt $updateSequence)",
        "Coroutine handles usable and distinct: True",
        "yield return null resumed on later frame: True",
        "Exact stopped coroutine remained cancelled: True",
        "Scene round trip completed: True",
        "Component instance retained across scene round trip: True",
        "Host root retained across scene round trip: True",
        "Fixture SHA-256: $((Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash)"
    )
    [IO.File]::WriteAllLines(
        (Join-Path $resultPath 'RUN-RESULT.txt'),
        $resultLines,
        (New-Object Text.UTF8Encoding($false))
    )
}
finally {
    if ((Test-Path -LiteralPath $evidencePath -PathType Leaf) -and
        -not (Test-Path -LiteralPath (Join-Path $resultPath `
            'RM21-RUNTIME-EVIDENCE.log'))) {
        Copy-Item -LiteralPath $evidencePath -Destination $resultPath
    }
    if ((Test-Path -LiteralPath $currentLogPath -PathType Leaf) -and
        -not (Test-Path -LiteralPath (Join-Path $resultPath `
            'DSPPluginManager.log'))) {
        Copy-Item -LiteralPath $currentLogPath -Destination $resultPath
    }
    Stop-InstalledGame
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
    if ($createdProxy -and
        (Test-Path -LiteralPath $proxyPath -PathType Leaf)) {
        Remove-Item -LiteralPath $proxyPath -Force
    }
    if ($null -ne $launcher) {
        $launcher.Dispose()
    }
    foreach ($protectedFile in $protectedFiles) {
        if ((Get-FileHash -LiteralPath $protectedFile `
                -Algorithm SHA256).Hash -cne
            $beforeHashes[$protectedFile]) {
            throw "Protected game file changed during RM-21: $protectedFile"
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

$restorationLines = @(
    'Manager install path removed: True',
    'Original Doorstop configuration restored: True',
    'Protected game and Unity assemblies unchanged: True',
    'DSP process stopped: True'
)
[IO.File]::AppendAllText(
    (Join-Path $resultPath 'RUN-RESULT.txt'),
    ($restorationLines -join [Environment]::NewLine) +
        [Environment]::NewLine,
    (New-Object Text.UTF8Encoding($false))
)
Write-Output "RM-21 installed check passed: $resultPath"
