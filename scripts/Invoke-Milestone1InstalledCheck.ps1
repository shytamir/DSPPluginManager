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
$managedPath = Join-Path $gamePath 'DSPGAME_Data\Managed'
$configurationPath = Join-Path $gamePath 'doorstop_config.ini'
$proxyPath = Join-Path $gamePath 'winhttp.dll'
$managerPath = Join-Path $gamePath 'DSPPluginManager'
$expectedManagerPath = [IO.Path]::GetFullPath($managerPath)
$bundleProxy = Join-Path $bundlePath 'winhttp.dll'
$bundleManager = Join-Path $bundlePath 'DSPPluginManager'
$checkpointPath = Join-Path $managerPath 'bootstrap-checkpoint.txt'
$currentLogPath = Join-Path $managerPath 'logs\DSPPluginManager.log'
$pluginPath = Join-Path $managerPath 'plugins'
$sentinelPath = Join-Path $managerPath 'candidate-code-executed.txt'
$contractTestExecutable = Join-Path $repositoryPath `
    'artifacts\contract-tests\DSPPluginManager.ContractTests.exe'
$consumerFixture = Join-Path $repositoryPath `
    'artifacts\fixtures\rm09-consumer\DSPPluginManager.RM09Consumer.dll'
$dependencyPath = Join-Path $managerPath 'dependencies'
$contractPath = Join-Path $managerPath 'DSPPluginManager.Contracts.dll'
$resultRoot = Join-Path $repositoryPath `
    'artifacts\milestone1-installed-check'
$resultPath = Join-Path $resultRoot (
    (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssfffZ')
)
$expectedPlanPath = Join-Path $resultPath 'EXPECTED-PLAN.txt'

foreach ($required in @(
        $gameExecutable,
        $SteamExecutable,
        $bundleProxy,
        (Join-Path $bundlePath 'doorstop_config.ini'),
        (Join-Path $bundleManager 'DSPPluginManager.dll'),
        $contractTestExecutable,
        $consumerFixture,
        (Join-Path $managedPath 'UnityEngine.CoreModule.dll')
    )) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required Milestone 1 input was not found: $required"
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

function Invoke-InstalledObservation {
    Stop-InstalledGame
    $launchers = New-Object 'Collections.Generic.List[Diagnostics.Process]'
    $lastLaunchRequest = [DateTime]::MinValue
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        $observedId = $null
        $windowObserved = $false
        $respondingObserved = $false
        $logContent = $null
        while ([DateTime]::UtcNow -lt $deadline) {
            if ([DateTime]::UtcNow -ge $lastLaunchRequest.AddSeconds(15)) {
                $launcher = Start-Process -FilePath $SteamExecutable `
                    -ArgumentList '-applaunch', $SteamAppId `
                    -WindowStyle Hidden -PassThru
                $launchers.Add($launcher)
                $lastLaunchRequest = [DateTime]::UtcNow
            }
            Start-Sleep -Milliseconds 500
            $process = Get-Process -Name 'DSPGAME' `
                -ErrorAction SilentlyContinue | Where-Object {
                    $_.Path -ceq $gameExecutable
                } | Sort-Object StartTime -Descending |
                Select-Object -First 1
            if ($null -eq $process) {
                continue
            }
            $observedId = $process.Id
            $process.Refresh()
            if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
                $windowObserved = $true
                if ($process.Responding) {
                    $respondingObserved = $true
                }
            }
            if ($respondingObserved -and
                (Test-Path -LiteralPath $checkpointPath -PathType Leaf) -and
                (Test-Path -LiteralPath $currentLogPath -PathType Leaf)) {
                try {
                    $logContent = Read-LiveUtf8File -Path $currentLogPath
                    if ($logContent.Contains(
                            'Pre-activation discovery completed:'
                        ) -and $logContent.Contains(
                            'Unity main-thread handoff completed.'
                        )) {
                        break
                    }
                }
                catch [IO.IOException] {
                    $logContent = $null
                }
            }
        }

        if (-not $windowObserved -or -not $respondingObserved) {
            throw 'Milestone 1 startup did not reach a responsive DSP window.'
        }
        if ($null -eq $logContent) {
            throw 'Milestone 1 startup did not expose its completed current-run log.'
        }
        return [PSCustomObject]@{
            ProcessId = $observedId
            WindowObserved = $windowObserved
            RespondingObserved = $respondingObserved
            LogContent = $logContent
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
$observation = $null

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

    & $contractTestExecutable `
        '--write-milestone1-fixture' `
        $contractPath `
        $consumerFixture `
        $dependencyPath `
        $managedPath `
        $pluginPath `
        $sentinelPath `
        $expectedPlanPath
    if ($LASTEXITCODE -ne 0) {
        throw 'The deterministic Milestone 1 fixture could not be generated.'
    }

    Copy-Item -LiteralPath (
        Join-Path $bundlePath 'doorstop_config.ini'
    ) -Destination $configurationPath -Force
    $installedConfiguration = Get-Content -LiteralPath $configurationPath
    if ($installedConfiguration -cnotcontains
        'targetAssembly=DSPPluginManager\DSPPluginManager.dll') {
        throw 'Installed Doorstop configuration does not target the manager.'
    }

    $observation = Invoke-InstalledObservation
    $checkpoint = [IO.File]::ReadAllLines($checkpointPath)
    $entryLines = @($checkpoint | Where-Object {
            $_ -match '\| event=managed-entry \|'
        })
    $handoffLines = @($checkpoint | Where-Object {
            $_ -match '\| event=unity-main-thread-handoff \|'
        })
    if ($entryLines.Count -ne 1 -or $handoffLines.Count -ne 1) {
        throw 'Installed startup did not record exactly one entry and one Unity handoff.'
    }
    if ($handoffLines[0] -cnotmatch
        'synchronizationContext=UnityEngine\.UnitySynchronizationContext') {
        throw 'The installed callback did not observe UnitySynchronizationContext.'
    }
    if (Test-Path -LiteralPath $sentinelPath) {
        throw 'Candidate code executed during installed discovery.'
    }
    if (-not $observation.LogContent.Contains(
            'runtimeLoadedCandidates=0.'
        )) {
        throw 'The installed log did not prove zero runtime-loaded candidates.'
    }

    $expectedPlan = [IO.File]::ReadAllLines($expectedPlanPath)
    $actualPlan = @([Regex]::Matches(
        $observation.LogContent,
        'DiscoveryPlan\|(?<plan>state=[^\r\n]+)'
    ) | ForEach-Object { $_.Groups['plan'].Value })
    if ($actualPlan.Count -ne $expectedPlan.Count -or
        (Compare-Object -ReferenceObject $expectedPlan `
            -DifferenceObject $actualPlan -SyncWindow 0).Count -ne 0) {
        throw "Installed discovery plan differs from the deterministic plan.`nExpected:`n$($expectedPlan -join "`n")`nActual:`n$($actualPlan -join "`n")"
    }

    Copy-Item -LiteralPath $checkpointPath -Destination $resultPath
    Copy-Item -LiteralPath $currentLogPath -Destination $resultPath
    $resultLines = @(
        "Installed executable: $gameExecutable",
        'Doorstop target: DSPPluginManager\DSPPluginManager.dll',
        "Manager SHA-256: $((Get-FileHash -LiteralPath (Join-Path $managerPath 'DSPPluginManager.dll') -Algorithm SHA256).Hash)",
        "Existing identical proxy reused: $proxyExisted",
        "Proxy SHA-256: $bundleProxyHash",
        "Process: $($observation.ProcessId)",
        "Responsive window: $($observation.RespondingObserved)",
        "Managed entry count: $($entryLines.Count)",
        "Unity callback count: $($handoffLines.Count)",
        "Current-run log: $currentLogPath",
        "Plan entry count: $($expectedPlan.Count)",
        'Runtime-loaded candidate count: 0',
        'Candidate execution sentinel observed: False',
        'Expected and installed plan match: True'
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
            throw "Protected game file changed during Milestone 1 check: $protectedFile"
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
    'Protected game and Unity assemblies unchanged: True'
)
[IO.File]::AppendAllText(
    (Join-Path $resultPath 'RUN-RESULT.txt'),
    ($restorationLines -join [Environment]::NewLine) +
        [Environment]::NewLine,
    (New-Object Text.UTF8Encoding($false))
)
Write-Output "Milestone 1 installed check passed: $resultPath"
