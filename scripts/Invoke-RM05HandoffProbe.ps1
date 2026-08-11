[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('runtime-attribute', 'cecil', 'disabled', 'early-failure')]
    [string]$Mode,

    [Parameter(Mandatory = $true)]
    [string]$GameRoot,

    [Parameter(Mandatory = $true)]
    [string]$ProbeDeployDirectory,

    [string]$SteamExecutable = 'C:\Program Files (x86)\Steam\steam.exe',

    [ValidatePattern('^\d+$')]
    [string]$SteamAppId = '1366540',

    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [ValidateRange(15, 180)]
    [int]$TimeoutSeconds = 75
)

$ErrorActionPreference = 'Stop'
$gamePath = (Resolve-Path -LiteralPath $GameRoot).Path
$deployPath = (Resolve-Path -LiteralPath $ProbeDeployDirectory).Path
$repositoryPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$gameExecutable = Join-Path $gamePath 'DSPGAME.exe'
$configurationPath = Join-Path $gamePath 'doorstop_config.ini'
$installPath = Join-Path $gamePath 'DSPPluginManager.RM05Probe'
$expectedInstallPath = [IO.Path]::GetFullPath($installPath)
$resultRoot = Join-Path $repositoryPath 'artifacts\rm05-probe\runs'
$runName = (Get-Date).ToUniversalTime().ToString(
    'yyyyMMddTHHmmssfffZ'
) + '-' + $Mode
$resultPath = Join-Path $resultRoot $runName

foreach ($requiredFile in @(
        $gameExecutable,
        $configurationPath,
        $SteamExecutable,
        (Join-Path $deployPath 'DSPPluginManager.RM05Probe.dll'),
        (Join-Path $deployPath 'DSPPluginManager.RM05Callback.dll'),
        (Join-Path $deployPath 'DSPPluginManager.RM05CecilHandoff.dll')
    )) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required RM-05 runtime input was not found: $requiredFile"
    }
}
if (Get-Process -Name 'DSPGAME' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -ceq $gameExecutable }) {
    throw 'DSPGAME is already running; refusing to alter bootstrap configuration.'
}
if (Test-Path -LiteralPath $installPath) {
    throw "Probe install path already exists: $installPath"
}

New-Item -ItemType Directory -Force -Path $resultPath | Out-Null
$configurationBytes = [IO.File]::ReadAllBytes($configurationPath)
$configurationHash = (
    Get-FileHash -Algorithm SHA256 -LiteralPath $configurationPath
).Hash
$corePath = Join-Path $gamePath `
    'DSPGAME_Data\Managed\UnityEngine.CoreModule.dll'
$coreHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $corePath).Hash
$existingEmergencyFiles = @(
    Get-ChildItem -LiteralPath $gamePath -File `
        -Filter 'DSPPluginManager-bootstrap-failure-*.txt' |
        ForEach-Object FullName
)
$process = $null
$launcher = $null
$observedProcessIds = New-Object 'Collections.Generic.List[int]'
$windowObserved = $false
$respondingObserved = $false
$steadyUpdateObserved = $false
$probeEvidencePath = $null
$processExitedNaturally = $false

try {
    Copy-Item -LiteralPath $deployPath -Destination $installPath -Recurse
    $effectiveMode = if ($Mode -ceq 'disabled') {
        'runtime-attribute'
    }
    else {
        $Mode
    }
    [IO.File]::WriteAllText(
        (Join-Path $installPath 'mode.txt'),
        $effectiveMode,
        (New-Object Text.UTF8Encoding($false))
    )

    $enabled = if ($Mode -ceq 'disabled') { 'false' } else { 'true' }
    $probeConfiguration = @"
[UnityDoorstop]
enabled=$enabled
targetAssembly=DSPPluginManager.RM05Probe\DSPPluginManager.RM05Probe.dll
redirectOutputLog=false
ignoreDisableSwitch=false
dllSearchPathOverride=
"@
    [IO.File]::WriteAllText(
        $configurationPath,
        $probeConfiguration,
        (New-Object Text.UTF8Encoding($false))
    )

    $launchStarted = [DateTime]::UtcNow
    $launcher = Start-Process -FilePath $SteamExecutable `
        -ArgumentList '-applaunch', $SteamAppId `
        -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 500
        if ($null -eq $process) {
            $process = Get-Process -Name 'DSPGAME' `
                -ErrorAction SilentlyContinue |
                Where-Object { $_.StartTime.ToUniversalTime() -ge $launchStarted } |
                Sort-Object StartTime -Descending |
                Select-Object -First 1
            if ($null -eq $process) {
                continue
            }
            if (-not $observedProcessIds.Contains($process.Id)) {
                $observedProcessIds.Add($process.Id)
            }
        }
        $process.Refresh()
        if ($process.HasExited) {
            $processExitedNaturally = $true
            $process.Dispose()
            $process = $null
            continue
        }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
            $windowObserved = $true
            if ($process.Responding) {
                $respondingObserved = $true
            }
        }

        $candidateEvidence = Get-ChildItem -LiteralPath $installPath -File `
            -Filter 'probe-evidence-*.log' -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($null -ne $candidateEvidence) {
            $probeEvidencePath = $candidateEvidence.FullName
            $evidence = [IO.File]::ReadAllText($probeEvidencePath)
            if ($evidence.Contains('event=steady-update')) {
                $steadyUpdateObserved = $true
            }
            if ($Mode -ceq 'early-failure' -and
                $evidence.Contains('event=preload-failure')) {
                break
            }
        }

        if (($Mode -ceq 'runtime-attribute' -or $Mode -ceq 'cecil') -and
            $windowObserved -and $respondingObserved -and
            $steadyUpdateObserved) {
            break
        }
        if ($Mode -ceq 'disabled' -and $windowObserved -and
            $respondingObserved -and
            ([DateTime]::UtcNow -gt $deadline.AddSeconds(-30))) {
            break
        }
    }

    if ($null -ne $probeEvidencePath -and
        (Test-Path -LiteralPath $probeEvidencePath -PathType Leaf)) {
        Copy-Item -LiteralPath $probeEvidencePath -Destination $resultPath
    }
    $newEmergencyFiles = @(
        Get-ChildItem -LiteralPath $gamePath -File `
            -Filter 'DSPPluginManager-bootstrap-failure-*.txt' |
            Where-Object { $existingEmergencyFiles -cnotcontains $_.FullName }
    )
    foreach ($emergencyFile in $newEmergencyFiles) {
        Copy-Item -LiteralPath $emergencyFile.FullName -Destination $resultPath
    }

    $result = @(
        "Mode: $Mode",
        "Observed process IDs: $($observedProcessIds -join ', ')",
        "Process exited naturally: $processExitedNaturally",
        "Window observed: $windowObserved",
        "Responding window observed: $respondingObserved",
        "Steady update observed: $steadyUpdateObserved",
        "Emergency files created: $($newEmergencyFiles.Count)",
        "Original doorstop config SHA-256: $configurationHash",
        "Original UnityEngine.CoreModule SHA-256: $coreHash"
    )
    [IO.File]::WriteAllLines(
        (Join-Path $resultPath 'RUN-RESULT.txt'),
        $result,
        (New-Object Text.UTF8Encoding($false))
    )
}
finally {
    [IO.File]::WriteAllBytes($configurationPath, $configurationBytes)
    $restoredHash = (
        Get-FileHash -Algorithm SHA256 -LiteralPath $configurationPath
    ).Hash
    if ($restoredHash -cne $configurationHash) {
        throw 'The original UnityDoorstop configuration was not restored exactly.'
    }
    if ($null -ne $launcher) {
        $launcher.Dispose()
    }

    if ($null -ne $process) {
        $process.Refresh()
        if (-not $process.HasExited) {
            $null = $process.CloseMainWindow()
            if (-not $process.WaitForExit(5000)) {
                Stop-Process -Id $process.Id -Force
                $process.WaitForExit()
            }
        }
        $process.Dispose()
    }

    $cleanupDeadline = [DateTime]::UtcNow.AddSeconds(20)
    $quietIntervals = 0
    while ([DateTime]::UtcNow -lt $cleanupDeadline -and
        $quietIntervals -lt 5) {
        $remainingProcesses = @(Get-Process -Name 'DSPGAME' `
            -ErrorAction SilentlyContinue | Where-Object {
                $_.Path -ceq $gameExecutable
            })
        if ($remainingProcesses.Count -eq 0) {
            $quietIntervals++
        }
        else {
            $quietIntervals = 0
            foreach ($remainingProcess in $remainingProcesses) {
                $null = $remainingProcess.CloseMainWindow()
                if (-not $remainingProcess.WaitForExit(5000)) {
                    Stop-Process -Id $remainingProcess.Id -Force
                    $remainingProcess.WaitForExit()
                }
                $remainingProcess.Dispose()
            }
        }
        Start-Sleep -Seconds 1
    }
    if (@(Get-Process -Name 'DSPGAME' -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -ceq $gameExecutable }).Count -ne 0) {
        throw 'A probe-launched DSP process remained after bounded cleanup.'
    }
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $corePath).Hash -cne
        $coreHash) {
        throw 'UnityEngine.CoreModule.dll changed during the RM-05 probe.'
    }

    $newEmergencyFiles = @(
        Get-ChildItem -LiteralPath $gamePath -File `
            -Filter 'DSPPluginManager-bootstrap-failure-*.txt' |
            Where-Object { $existingEmergencyFiles -cnotcontains $_.FullName }
    )
    foreach ($emergencyFile in $newEmergencyFiles) {
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

Write-Output "RM-05 probe run captured: $resultPath"
