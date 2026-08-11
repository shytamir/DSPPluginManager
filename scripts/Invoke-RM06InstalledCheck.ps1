[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,

    [string]$BundleRoot = '',

    [string]$SteamExecutable = 'C:\Program Files (x86)\Steam\steam.exe',

    [ValidatePattern('^\d+$')]
    [string]$SteamAppId = '1366540',

    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [ValidateRange(30, 180)]
    [int]$TimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'
$repositoryPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$gamePath = (Resolve-Path -LiteralPath $GameRoot).Path
if ([string]::IsNullOrWhiteSpace($BundleRoot)) {
    $BundleRoot = Join-Path $repositoryPath 'artifacts\bootstrap-bundle'
}
$bundlePath = (Resolve-Path -LiteralPath $BundleRoot).Path
$gameExecutable = Join-Path $gamePath 'DSPGAME.exe'
$configurationPath = Join-Path $gamePath 'doorstop_config.ini'
$proxyPath = Join-Path $gamePath 'winhttp.dll'
$managerPath = Join-Path $gamePath 'DSPPluginManager'
$expectedManagerPath = [IO.Path]::GetFullPath($managerPath)
$bundleProxy = Join-Path $bundlePath 'winhttp.dll'
$bundleManager = Join-Path $bundlePath 'DSPPluginManager'
$checkpointPath = Join-Path $managerPath 'bootstrap-checkpoint.txt'
$resultRoot = Join-Path $repositoryPath 'artifacts\rm06-installed-check'
$resultPath = Join-Path $resultRoot (
    (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssfffZ')
)

foreach ($required in @(
        $gameExecutable,
        $SteamExecutable,
        $bundleProxy,
        (Join-Path $bundlePath 'doorstop_config.ini'),
        (Join-Path $bundleManager 'DSPPluginManager.dll')
    )) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required RM-06 input was not found: $required"
    }
}
if (Test-Path -LiteralPath $managerPath) {
    throw "Manager install path already exists: $managerPath"
}
if (@(Get-Process -Name 'DSPGAME' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -ceq $gameExecutable }).Count -ne 0) {
    throw 'The installed DSP executable is already running.'
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
    $quietDeadline = [DateTime]::UtcNow.AddSeconds(15)
    $quietIntervals = 0
    while ([DateTime]::UtcNow -lt $quietDeadline -and
        $quietIntervals -lt 4) {
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
        throw 'The installed DSP process did not remain stopped.'
    }
}

function Invoke-StartupObservation {
    param(
        [Parameter(Mandatory = $true)][string]$Mode,
        [Parameter(Mandatory = $true)][bool]$ExpectCheckpoint
    )

    Stop-InstalledGame
    $launchers = New-Object 'Collections.Generic.List[Diagnostics.Process]'
    $lastLaunchRequest = [DateTime]::MinValue
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        $observedId = $null
        $processObservedAt = $null
        $windowObserved = $false
        $respondingObserved = $false
        while ([DateTime]::UtcNow -lt $deadline) {
            if ($null -eq $processObservedAt -and
                [DateTime]::UtcNow -ge $lastLaunchRequest.AddSeconds(15)) {
                $launcher = Start-Process -FilePath $SteamExecutable `
                    -ArgumentList '-applaunch', $SteamAppId -PassThru
                $launchers.Add($launcher)
                $lastLaunchRequest = [DateTime]::UtcNow
            }
            Start-Sleep -Milliseconds 500
            $process = Get-Process -Name 'DSPGAME' -ErrorAction SilentlyContinue |
                Where-Object {
                    $_.Path -ceq $gameExecutable
                } |
                Sort-Object StartTime -Descending |
                Select-Object -First 1
            if ($null -eq $process) {
                continue
            }
            $observedId = $process.Id
            if ($null -eq $processObservedAt) {
                $processObservedAt = [DateTime]::UtcNow
            }
            $process.Refresh()
            if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
                $windowObserved = $true
                if ($process.Responding) {
                    $respondingObserved = $true
                }
            }
            $checkpointObserved = Test-Path -LiteralPath $checkpointPath `
                -PathType Leaf
            if ($ExpectCheckpoint -and $checkpointObserved -and
                $respondingObserved) {
                $content = [IO.File]::ReadAllText($checkpointPath)
                if ($content.Contains('event=unity-main-thread-handoff')) {
                    break
                }
            }
            if (-not $ExpectCheckpoint -and $respondingObserved -and
                [DateTime]::UtcNow -gt $processObservedAt.AddSeconds(20)) {
                break
            }
        }

        if (-not $windowObserved -or -not $respondingObserved) {
            throw "$Mode startup did not reach a responsive installed DSP window."
        }
        $checkpointExists = Test-Path -LiteralPath $checkpointPath -PathType Leaf
        if ($checkpointExists -ne $ExpectCheckpoint) {
            throw "$Mode checkpoint expectation failed: expected=$ExpectCheckpoint actual=$checkpointExists"
        }
        return [PSCustomObject]@{
            Mode = $Mode
            ProcessId = $observedId
            WindowObserved = $windowObserved
            RespondingObserved = $respondingObserved
            CheckpointObserved = $checkpointExists
        }
    }
    finally {
        foreach ($launcher in $launchers) {
            $launcher.Dispose()
        }
        Stop-InstalledGame
    }
}

$configurationExisted = Test-Path -LiteralPath $configurationPath -PathType Leaf
$configurationBytes = if ($configurationExisted) {
    [IO.File]::ReadAllBytes($configurationPath)
}
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
$enabledResult = $null
$disabledResult = $null
$removalResult = $null

New-Item -ItemType Directory -Force -Path $resultPath | Out-Null
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

    Copy-Item -LiteralPath (
        Join-Path $bundlePath 'doorstop_config.ini'
    ) -Destination $configurationPath -Force
    $enabledResult = Invoke-StartupObservation `
        -Mode 'enabled' -ExpectCheckpoint $true

    $checkpoint = [IO.File]::ReadAllLines($checkpointPath)
    $entryLines = @($checkpoint | Where-Object {
            $_ -match '\| event=managed-entry \|'
        })
    $handoffLines = @($checkpoint | Where-Object {
            $_ -match '\| event=unity-main-thread-handoff \|'
        })
    if ($entryLines.Count -ne 1 -or $handoffLines.Count -ne 1) {
        throw 'Enabled startup did not record exactly one entry and one Unity handoff.'
    }
    if ($handoffLines[0] -cnotmatch
        'synchronizationContext=UnityEngine\.UnitySynchronizationContext') {
        throw 'The selected callback did not observe UnitySynchronizationContext.'
    }
    $entryThread = [Regex]::Match($entryLines[0], 'thread=(\d+)').Groups[1].Value
    $handoffThread = [Regex]::Match(
        $handoffLines[0],
        'thread=(\d+)'
    ).Groups[1].Value
    if ([string]::IsNullOrWhiteSpace($entryThread) -or
        $entryThread -cne $handoffThread) {
        throw 'The Unity handoff did not run on the established bootstrap thread.'
    }
    Copy-Item -LiteralPath $checkpointPath -Destination $resultPath

    Remove-Item -LiteralPath $checkpointPath -Force
    $disabledConfig = @'
[UnityDoorstop]
enabled=false
targetAssembly=DSPPluginManager\DSPPluginManager.dll
redirectOutputLog=false
ignoreDisableSwitch=false
dllSearchPathOverride=
'@
    [IO.File]::WriteAllText(
        $configurationPath,
        $disabledConfig,
        (New-Object Text.UTF8Encoding($false))
    )
    $disabledResult = Invoke-StartupObservation `
        -Mode 'disabled' -ExpectCheckpoint $false

    $resolvedManager = (Resolve-Path -LiteralPath $managerPath).Path
    if ($resolvedManager -cne $expectedManagerPath -or
        (Split-Path -Parent $resolvedManager) -cne $gamePath) {
        throw "Refusing to remove unexpected manager path: $resolvedManager"
    }
    Remove-Item -LiteralPath $resolvedManager -Recurse -Force
    $removalResult = Invoke-StartupObservation `
        -Mode 'clean-removal' -ExpectCheckpoint $false

    $resultLines = @(
        "Installed executable: $gameExecutable",
        "Existing identical proxy reused: $proxyExisted",
        "Proxy SHA-256: $bundleProxyHash",
        "Enabled process: $($enabledResult.ProcessId)",
        "Enabled responsive: $($enabledResult.RespondingObserved)",
        "Enabled checkpoint: $($enabledResult.CheckpointObserved)",
        "Managed entry count: $($entryLines.Count)",
        "Unity callback count: $($handoffLines.Count)",
        "Unity callback context: UnityEngine.UnitySynchronizationContext",
        "Entry/callback thread: $entryThread",
        "Disabled process: $($disabledResult.ProcessId)",
        "Disabled responsive: $($disabledResult.RespondingObserved)",
        "Disabled checkpoint: $($disabledResult.CheckpointObserved)",
        "Removal process: $($removalResult.ProcessId)",
        "Removal responsive: $($removalResult.RespondingObserved)",
        "Removal checkpoint: $($removalResult.CheckpointObserved)"
    )
    [IO.File]::WriteAllLines(
        (Join-Path $resultPath 'RUN-RESULT.txt'),
        $resultLines,
        (New-Object Text.UTF8Encoding($false))
    )
}
finally {
    Stop-InstalledGame
    if (Test-Path -LiteralPath $managerPath) {
        $resolvedManager = (Resolve-Path -LiteralPath $managerPath).Path
        if ($resolvedManager -cne $expectedManagerPath -or
            (Split-Path -Parent $resolvedManager) -cne $gamePath) {
            throw "Refusing to remove unexpected manager path: $resolvedManager"
        }
        Remove-Item -LiteralPath $resolvedManager -Recurse -Force
    }
    if ($configurationExisted) {
        [IO.File]::WriteAllBytes($configurationPath, $configurationBytes)
        if ((Get-FileHash -LiteralPath $configurationPath `
                -Algorithm SHA256).Hash -cne $configurationHash) {
            throw 'The original Doorstop configuration was not restored exactly.'
        }
    }
    elseif (Test-Path -LiteralPath $configurationPath) {
        Remove-Item -LiteralPath $configurationPath -Force
    }
    if ($createdProxy -and
        (Test-Path -LiteralPath $proxyPath -PathType Leaf)) {
        Remove-Item -LiteralPath $proxyPath -Force
    }

    foreach ($protectedFile in $protectedFiles) {
        $afterHash = (
            Get-FileHash -LiteralPath $protectedFile -Algorithm SHA256
        ).Hash
        if ($afterHash -cne $beforeHashes[$protectedFile]) {
            throw "Protected game file changed during RM-06 check: $protectedFile"
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

Write-Output "RM-06 installed check passed: $resultPath"
