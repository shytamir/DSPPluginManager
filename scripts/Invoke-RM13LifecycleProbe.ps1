[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,

    [Parameter(Mandatory = $true)]
    [string]$ProbeDeployDirectory,

    [string]$SteamExecutable = 'C:\Program Files (x86)\Steam\steam.exe',

    [ValidatePattern('^\d+$')]
    [string]$SteamAppId = '1366540',

    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [ValidateRange(30, 180)]
    [int]$TimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'
$gamePath = (Resolve-Path -LiteralPath $GameRoot).Path
$deployPath = (Resolve-Path -LiteralPath $ProbeDeployDirectory).Path
$repositoryPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$gameExecutable = Join-Path $gamePath 'DSPGAME.exe'
$configurationPath = Join-Path $gamePath 'doorstop_config.ini'
$proxyPath = Join-Path $gamePath 'winhttp.dll'
$installPath = Join-Path $gamePath 'DSPPluginManager.RM13Probe'
$expectedInstallPath = [IO.Path]::GetFullPath($installPath)
$evidencePath = Join-Path $installPath 'probe-evidence.log'
$resultRoot = Join-Path $repositoryPath 'artifacts\rm13-probe\runs'
$resultPath = Join-Path $resultRoot (
    (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssfffZ')
)

foreach ($requiredFile in @(
        $gameExecutable,
        $proxyPath,
        $SteamExecutable,
        (Join-Path $deployPath 'DSPPluginManager.RM13Probe.dll'),
        (Join-Path $deployPath 'DSPPluginManager.RM13Callback.dll'),
        (Join-Path $deployPath 'DSPPluginManager.RM13CecilHandoff.dll')
    )) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required RM-13 runtime input was not found: $requiredFile"
    }
}
if (Test-Path -LiteralPath $installPath) {
    throw "Probe install path already exists: $installPath"
}
if (@(Get-Process -Name 'DSPGAME' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -ceq $gameExecutable }).Count -ne 0) {
    throw 'DSPGAME is already running; refusing to alter bootstrap configuration.'
}

function Stop-InstalledGame {
    $processes = @(Get-Process -Name 'DSPGAME' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -ceq $gameExecutable })
    foreach ($process in $processes) {
        $null = $process.CloseMainWindow()
        if (-not $process.WaitForExit(5000)) {
            Stop-Process -Id $process.Id -Force
            $process.WaitForExit()
        }
        $process.Dispose()
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
        throw 'The RM-13 DSP process did not remain stopped.'
    }
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

function Event-Lines {
    param(
        [Parameter(Mandatory = $true)][string[]]$Lines,
        [Parameter(Mandatory = $true)][string]$Event
    )

    return @($Lines | Where-Object {
            $_ -match ('\| event=' + [Regex]::Escape($Event) + '( \||$)')
        })
}

$configurationExisted = Test-Path -LiteralPath $configurationPath -PathType Leaf
$configurationBytes = if ($configurationExisted) {
    [IO.File]::ReadAllBytes($configurationPath)
}
$configurationHash = if ($configurationExisted) {
    (Get-FileHash -Algorithm SHA256 -LiteralPath $configurationPath).Hash
}
$protectedFiles = @(
    $gameExecutable,
    (Join-Path $gamePath 'DSPGAME_Data\Managed\Assembly-CSharp.dll'),
    (Join-Path $gamePath 'DSPGAME_Data\Managed\UnityEngine.CoreModule.dll')
)
$beforeHashes = @{}
foreach ($protectedFile in $protectedFiles) {
    $beforeHashes[$protectedFile] = (
        Get-FileHash -Algorithm SHA256 -LiteralPath $protectedFile
    ).Hash
}
$existingEmergencyFiles = @(
    Get-ChildItem -LiteralPath $gamePath -File `
        -Filter 'DSPPluginManager-bootstrap-failure-*.txt' |
        ForEach-Object FullName
)
$launcher = $null
$process = $null
$windowObserved = $false
$respondingObserved = $false
$evidence = $null

New-Item -ItemType Directory -Force -Path $resultPath | Out-Null
try {
    Copy-Item -LiteralPath $deployPath -Destination $installPath -Recurse
    $probeConfiguration = @'
[UnityDoorstop]
enabled=true
targetAssembly=DSPPluginManager.RM13Probe\DSPPluginManager.RM13Probe.dll
redirectOutputLog=false
ignoreDisableSwitch=false
dllSearchPathOverride=
'@
    [IO.File]::WriteAllText(
        $configurationPath,
        $probeConfiguration,
        (New-Object Text.UTF8Encoding($false))
    )

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
            throw 'DSP exited before the RM-13 probe completed.'
        }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
            $windowObserved = $true
            if ($process.Responding) {
                $respondingObserved = $true
            }
        }
        if (Test-Path -LiteralPath $evidencePath -PathType Leaf) {
            try {
                $evidence = Read-LiveUtf8File -Path $evidencePath
            }
            catch [IO.IOException] {
                $evidence = $null
            }
            if ($respondingObserved -and $null -ne $evidence -and
                $evidence.Contains('event=probe-complete')) {
                break
            }
        }
    }

    if (-not $windowObserved -or -not $respondingObserved) {
        throw 'RM-13 did not reach a responsive DSP window.'
    }
    if ($null -eq $evidence -or
        -not $evidence.Contains('event=probe-complete')) {
        throw 'RM-13 lifecycle evidence did not complete.'
    }

    $lines = @($evidence -split '\r?\n' | Where-Object {
            -not [string]::IsNullOrWhiteSpace($_)
        })
    $singleEvents = @(
        'unity-callback',
        'supervisor-awake',
        'supervisor-start',
        'direct-awake-success-enter',
        'direct-awake-success-complete',
        'direct-awake-failure-enter',
        'direct-stop-success-awake',
        'direct-stop-failure-awake',
        'direct-destroy-request-start',
        'direct-destroy-request-return',
        'direct-destroy-success-enter',
        'direct-destroy-success-complete',
        'direct-destroy-failure-enter',
        'explicit-start-failure-catch',
        'explicit-stop-success-return',
        'explicit-stop-failure-catch',
        'lifecycle-summary',
        'probe-complete'
    )
    foreach ($eventName in $singleEvents) {
        $count = @(Event-Lines -Lines $lines -Event $eventName).Count
        if ($count -ne 1) {
            throw "RM-13 expected one '$eventName' event; observed $count."
        }
    }
    foreach ($unexpected in @(
            'direct-awake-failure-complete',
            'direct-destroy-failure-complete',
            'explicit-start-failure-return',
            'explicit-stop-failure-return'
        )) {
        if (@(Event-Lines -Lines $lines -Event $unexpected).Count -ne 0) {
            throw "RM-13 unexpectedly observed '$unexpected'."
        }
    }

    $directAwakeReturned = @(
        Event-Lines -Lines $lines -Event 'direct-awake-failure-add-return'
    ).Count -eq 1
    $directAwakeCaught = @(
        Event-Lines -Lines $lines -Event 'direct-awake-failure-add-catch'
    ).Count -eq 1
    if ($directAwakeReturned -eq $directAwakeCaught) {
        throw 'RM-13 could not determine the direct Awake AddComponent outcome.'
    }

    $unityLogs = @(Event-Lines -Lines $lines -Event 'unity-log-exception')
    if ($unityLogs.Count -lt 2 -or
        -not ($unityLogs -join "`n").Contains('RM-13 direct Awake failure') -or
        -not ($unityLogs -join "`n").Contains('RM-13 direct OnDestroy failure')) {
        throw 'RM-13 did not retain both direct Unity exception diagnostics.'
    }
    $explicitStartFailure = @(
        Event-Lines -Lines $lines -Event 'explicit-start-failure-catch'
    )[0]
    $explicitStopFailure = @(
        Event-Lines -Lines $lines -Event 'explicit-stop-failure-catch'
    )[0]
    if (-not $explicitStartFailure.Contains(
            'RM-13 explicit activation failure'
        ) -or -not $explicitStartFailure.Contains(
            'The second line proves complete diagnostics.'
        ) -or -not $explicitStopFailure.Contains(
            'RM-13 explicit cleanup failure'
        ) -or -not $explicitStopFailure.Contains(
            'The second line proves complete diagnostics.'
        )) {
        throw 'RM-13 explicit failure diagnostics were incomplete.'
    }

    $summary = @(Event-Lines -Lines $lines -Event 'lifecycle-summary')[0]
    foreach ($expected in @(
            'directAwakeSuccessEnter=1',
            'directAwakeSuccessComplete=1',
            'directAwakeFailureEnter=1',
            'directAwakeFailureComplete=0',
            'directDestroySuccessEnter=1',
            'directDestroySuccessComplete=1',
            'directDestroyFailureEnter=1',
            'directDestroyFailureComplete=0',
            'explicitStartSuccess=3',
            'explicitStartFailure=1',
            'explicitStopSuccess=1',
            'explicitStopFailure=1'
        )) {
        if (-not $summary.Contains($expected)) {
            throw "RM-13 summary is missing '$expected'."
        }
    }

    $preloadLine = @(Event-Lines -Lines $lines -Event 'preload-main')[0]
    $preloadThread = [Regex]::Match(
        $preloadLine,
        '\| thread=(\d+) \|'
    ).Groups[1].Value
    $lifecycleLines = @($lines | Where-Object {
            $_ -match '\| event=(unity-callback|supervisor-|direct-|explicit-|lifecycle-summary|probe-complete)'
        })
    $lifecycleThreads = @($lifecycleLines | ForEach-Object {
            [Regex]::Match($_, '\| thread=(\d+) \|').Groups[1].Value
        } | Sort-Object -Unique)
    if ([string]::IsNullOrWhiteSpace($preloadThread) -or
        $lifecycleThreads.Count -ne 1 -or
        $lifecycleThreads[0] -cne $preloadThread) {
        throw 'RM-13 lifecycle cases did not remain on the Unity/bootstrap thread.'
    }

    $destroyReturnIndex = [Array]::IndexOf(
        $lines,
        @(Event-Lines -Lines $lines -Event 'direct-destroy-request-return')[0]
    )
    $destroyCallbackIndex = [Array]::IndexOf(
        $lines,
        @(Event-Lines -Lines $lines -Event 'direct-destroy-success-enter')[0]
    )
    $directDestroyReturnedBeforeCallback =
        $destroyReturnIndex -lt $destroyCallbackIndex
    if (-not $directDestroyReturnedBeforeCallback) {
        throw 'RM-13 ordinary Destroy did not return before OnDestroy dispatch.'
    }

    Copy-Item -LiteralPath $evidencePath -Destination $resultPath
    Copy-Item -LiteralPath (
        Join-Path (Split-Path -Parent $deployPath) 'PROBE-BUILD-INFO.txt'
    ) -Destination $resultPath
    $resultLines = @(
        "Installed executable: $gameExecutable",
        "Process: $($process.Id)",
        "Responsive window: $respondingObserved",
        "Unity/bootstrap thread: $preloadThread",
        'Unity callback count: 1',
        "Direct Awake failure escaped AddComponent: $directAwakeCaught",
        "Direct Awake failure returned from AddComponent: $directAwakeReturned",
        'Direct Awake failure visible only through Unity log callback: True',
        "Ordinary Destroy returned before OnDestroy callback: $directDestroyReturnedBeforeCallback",
        'Direct OnDestroy failure visible only through Unity log callback: True',
        'Explicit activation failure caught with complete exception: True',
        'Explicit cleanup failure caught with complete exception: True',
        'Success and throwing callback counts validated: True'
    )
    [IO.File]::WriteAllLines(
        (Join-Path $resultPath 'RUN-RESULT.txt'),
        $resultLines,
        (New-Object Text.UTF8Encoding($false))
    )
}
finally {
    if ($configurationExisted) {
        [IO.File]::WriteAllBytes($configurationPath, $configurationBytes)
        if ((Get-FileHash -Algorithm SHA256 `
                -LiteralPath $configurationPath).Hash -cne
            $configurationHash) {
            throw 'The original Doorstop configuration was not restored exactly.'
        }
    }
    elseif (Test-Path -LiteralPath $configurationPath) {
        Remove-Item -LiteralPath $configurationPath -Force
    }
    if ($null -ne $launcher) {
        $launcher.Dispose()
    }
    Stop-InstalledGame

    foreach ($protectedFile in $protectedFiles) {
        if ((Get-FileHash -Algorithm SHA256 `
                -LiteralPath $protectedFile).Hash -cne
            $beforeHashes[$protectedFile]) {
            throw "Protected game file changed during RM-13: $protectedFile"
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
    if (Test-Path -LiteralPath $installPath) {
        $resolvedInstall = (Resolve-Path -LiteralPath $installPath).Path
        if ($resolvedInstall -cne $expectedInstallPath -or
            (Split-Path -Parent $resolvedInstall) -cne $gamePath) {
            throw "Refusing to remove unexpected probe path: $resolvedInstall"
        }
        Remove-Item -LiteralPath $resolvedInstall -Recurse -Force
    }
}

$restorationLines = @(
    'Probe install path removed: True',
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
Write-Output "RM-13 lifecycle probe passed: $resultPath"
