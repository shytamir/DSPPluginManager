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

    [ValidateRange(45, 180)]
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
$configPath = Join-Path $managerPath 'config'
$writablePath = Join-Path $managerPath 'writable'
$mirrorRoot = Join-Path $writablePath 'fixture.rm34.mirror'
$guideRoot = Join-Path $writablePath 'fixture.rm34.guide'
$mirrorConfig = Join-Path $configPath 'fixture.rm34.mirror.cfg'
$guideConfig = Join-Path $configPath 'fixture.rm34.guide.cfg'
$currentLogPath = Join-Path $managerPath 'logs\DSPPluginManager.log'
$checkpointPath = Join-Path $managerPath 'bootstrap-checkpoint.txt'
$mirrorFixture = Join-Path $repositoryPath `
    'artifacts\fixtures\rm32-mirror-qualification\DSPPluginManager.RM32MirrorQualification.dll'
$guideFixture = Join-Path $repositoryPath `
    'artifacts\fixtures\rm32-guide-qualification\DSPPluginManager.RM32GuideQualification.dll'
$failureFixture = Join-Path $repositoryPath `
    'artifacts\fixtures\rm20-activation-failure\DSPPluginManager.RM20ActivationFailure.dll'
$managedPath = Join-Path $gamePath 'DSPGAME_Data\Managed'
$resultRoot = Join-Path $repositoryPath `
    'artifacts\milestone3-installed-check'
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
        $mirrorFixture,
        $guideFixture,
        $failureFixture
    )) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required Milestone 3 input was not found: $required"
    }
}
if (Test-Path -LiteralPath $managerPath) {
    throw "Manager install path already exists: $managerPath"
}
if (@(Get-Process -Name 'DSPGAME' -ErrorAction SilentlyContinue).Count -ne 0) {
    throw 'A DSPGAME process is already running; refusing to alter the installed bootstrap configuration.'
}

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Threading;
public static class RM34Keyboard
{
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint count,
        INPUT[] inputs,
        int size
    );

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion input;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT keyboard;
        [FieldOffset(0)] public MOUSEINPUT mouse;
        [FieldOffset(0)] public HARDWAREINPUT hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort virtualKey;
        public ushort scanCode;
        public uint flags;
        public uint time;
        public UIntPtr extraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint flags;
        public uint time;
        public UIntPtr extraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint message;
        public ushort parameterLow;
        public ushort parameterHigh;
    }

    public static void SendChord(byte[] keys, int holdMilliseconds)
    {
        INPUT[] down = new INPUT[keys.Length];
        INPUT[] up = new INPUT[keys.Length];
        for (int index = 0; index < keys.Length; index++)
        {
            down[index].type = 1;
            down[index].input.keyboard.virtualKey = keys[index];
            up[index].type = 1;
            up[index].input.keyboard.virtualKey =
                keys[keys.Length - index - 1];
            up[index].input.keyboard.flags = 2;
        }
        int size = Marshal.SizeOf(typeof(INPUT));
        if (SendInput((uint)down.Length, down, size) != down.Length)
            throw new InvalidOperationException(
                "Windows did not accept the complete key-down chord."
            );
        Thread.Sleep(holdMilliseconds);
        if (SendInput((uint)up.Length, up, size) != up.Length)
            throw new InvalidOperationException(
                "Windows did not accept the complete key-up chord."
            );
    }
}
'@

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
        throw "Milestone 3 evidence was not written: $Path"
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
        throw "Milestone 3 evidence expected $Name=$Expected."
    }
}

function Wait-File {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][Diagnostics.Process]$Process,
        [Parameter(Mandatory = $true)][DateTime]$Deadline
    )
    while ([DateTime]::UtcNow -lt $Deadline) {
        if (Test-Path -LiteralPath $Path -PathType Leaf) {
            return
        }
        $Process.Refresh()
        if ($Process.HasExited) {
            throw "DSP exited before evidence appeared: $Path"
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Timed out waiting for installed evidence: $Path"
}

function Send-KeyChord {
    param(
        [Parameter(Mandatory = $true)][Diagnostics.Process]$Process,
        [Parameter(Mandatory = $true)][byte[]]$VirtualKeys
    )
    $Process.Refresh()
    $activated = $false
    if ($Process.MainWindowHandle -ne [IntPtr]::Zero) {
        $shell = New-Object -ComObject WScript.Shell
        try {
            $activated = $shell.AppActivate($Process.Id)
        }
        finally {
            [Runtime.InteropServices.Marshal]::ReleaseComObject($shell) |
                Out-Null
        }
        if (-not $activated) {
            $activated = [RM34Keyboard]::SetForegroundWindow(
                $Process.MainWindowHandle
            )
        }
    }
    if (-not $activated) {
        throw 'The installed DSP window could not receive RM-34 input.'
    }
    Start-Sleep -Milliseconds 250
    [RM34Keyboard]::SendChord($VirtualKeys, 350)
    Start-Sleep -Milliseconds 250
}

function Send-UntilEvidence {
    param(
        [Parameter(Mandatory = $true)][Diagnostics.Process]$Process,
        [Parameter(Mandatory = $true)][byte[]]$VirtualKeys,
        [Parameter(Mandatory = $true)][string]$EvidencePath,
        [Parameter(Mandatory = $true)][DateTime]$Deadline
    )
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        Send-KeyChord $Process $VirtualKeys
        $attemptDeadline = [DateTime]::UtcNow.AddSeconds(2)
        while ([DateTime]::UtcNow -lt $attemptDeadline -and
            [DateTime]::UtcNow -lt $Deadline) {
            if (Test-Path -LiteralPath $EvidencePath -PathType Leaf) {
                return
            }
            $Process.Refresh()
            if ($Process.HasExited) {
                throw "DSP exited before evidence appeared: $EvidencePath"
            }
            Start-Sleep -Milliseconds 100
        }
    }
    Wait-File $EvidencePath $Process $Deadline
}

function Copy-RunEvidence {
    param([Parameter(Mandatory = $true)][int]$Run)

    $destination = Join-Path $resultPath "RUN-$Run"
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    foreach ($source in @(
            (Join-Path $mirrorRoot "RM34-MIRROR-$Run.log"),
            (Join-Path $guideRoot "RM34-GUIDE-$Run.log"),
            $mirrorConfig,
            $guideConfig,
            $currentLogPath,
            $checkpointPath
        )) {
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "Milestone 3 run evidence is missing: $source"
        }
        Copy-Item -LiteralPath $source -Destination $destination
    }
}

function Invoke-InstalledRun {
    param([Parameter(Mandatory = $true)][int]$Run)

    [IO.File]::WriteAllText(
        (Join-Path $mirrorRoot 'RUN.txt'),
        $Run.ToString(),
        (New-Object Text.UTF8Encoding($false))
    )
    [IO.File]::WriteAllText(
        (Join-Path $guideRoot 'RUN.txt'),
        $Run.ToString(),
        (New-Object Text.UTF8Encoding($false))
    )
    $mirrorActivated = Join-Path $mirrorRoot "ACTIVATED-$Run.txt"
    $guideActivated = Join-Path $guideRoot "ACTIVATED-$Run.txt"
    $mirrorPoll = Join-Path $mirrorRoot "POLL-$Run.txt"
    $guidePoll = Join-Path $guideRoot "POLL-$Run.txt"
    $launchStarted = [DateTime]::UtcNow
    $launcher = Start-Process -FilePath $SteamExecutable `
        -ArgumentList '-applaunch', $SteamAppId `
        -WindowStyle Hidden -PassThru
    $process = $null
    $respondingObserved = $false
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        while ([DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 250
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
                break
            }
            if ($process.HasExited) {
                throw "Milestone 3 run $Run exited before becoming responsive."
            }
        }
        if ($null -eq $process -or -not $respondingObserved) {
            throw "Milestone 3 run $Run did not reach a responsive DSP window."
        }
        Wait-File $mirrorActivated $process $deadline
        Wait-File $guideActivated $process $deadline

        if ($Run -eq 1) {
            Send-KeyChord $process ([byte[]](0x10, 0x41, 0x78))
            Start-Sleep -Milliseconds 750
            if (Test-Path -LiteralPath $mirrorPoll -PathType Leaf) {
                throw 'The Mirror exact shortcut accepted an additional keyboard key.'
            }
        }
        Send-UntilEvidence `
            $process ([byte[]](0x10, 0x78)) $mirrorPoll $deadline
        Send-UntilEvidence `
            $process ([byte[]](0x77)) $guidePoll $deadline

        while ([DateTime]::UtcNow -lt $deadline) {
            $process.Refresh()
            if ($process.HasExited) {
                break
            }
            Start-Sleep -Milliseconds 250
        }
        if (-not $process.HasExited) {
            throw "Milestone 3 run $Run did not complete its orderly exit."
        }
        Copy-RunEvidence $Run
        return [pscustomobject]@{
            ProcessId = $process.Id
            Responding = $respondingObserved
        }
    }
    finally {
        if ($null -ne $process) {
            $process.Dispose()
        }
        $launcher.Dispose()
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
$runs = @()

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
    New-Item -ItemType Directory -Force -Path `
        $pluginPath, $configPath, $mirrorRoot, $guideRoot | Out-Null
    [IO.File]::WriteAllBytes(
        (Join-Path $managerPath 'bootstrap-checkpoint.enabled'),
        [byte[]]@()
    )
    Copy-Item -LiteralPath $mirrorFixture -Destination $pluginPath
    Copy-Item -LiteralPath $guideFixture -Destination $pluginPath
    Copy-Item -LiteralPath $failureFixture -Destination $pluginPath
    [IO.File]::WriteAllText(
        $mirrorConfig,
        "[Diagnostics]`nEnabled = true`nShortcut = F9 + LeftShift`n",
        (New-Object Text.UTF8Encoding($false))
    )
    [IO.File]::WriteAllText(
        $guideConfig,
        "[General]`nShow Panel = false`nToggle Shortcut = `n`n" +
            "[Phase Selection]`nCurrent = current`nLegacy = legacy`n",
        (New-Object Text.UTF8Encoding($false))
    )
    Copy-Item -LiteralPath (
        Join-Path $bundlePath 'doorstop_config.ini'
    ) -Destination $configurationPath -Force

    $runs += Invoke-InstalledRun 1
    $runs += Invoke-InstalledRun 2

    $mirror1 = Read-KeyValues (
        Join-Path $mirrorRoot 'RM34-MIRROR-1.log'
    )
    $mirror2 = Read-KeyValues (
        Join-Path $mirrorRoot 'RM34-MIRROR-2.log'
    )
    $guide1 = Read-KeyValues (
        Join-Path $guideRoot 'RM34-GUIDE-1.log'
    )
    $guide2 = Read-KeyValues (
        Join-Path $guideRoot 'RM34-GUIDE-2.log'
    )
    foreach ($evidence in @($mirror1, $mirror2, $guide1, $guide2)) {
        Require-Value $evidence 'pollCount' '1'
        Require-Value $evidence 'bepInExLoadedAtActivation' 'False'
        Require-Value $evidence 'bepInExLoadedAtCleanup' 'False'
        Require-Value $evidence 'loggerAvailable' 'True'
        Require-Value $evidence 'configurationAvailable' 'True'
        Require-Value $evidence 'writableRootAvailable' 'True'
        Require-Value $evidence 'componentAvailable' 'True'
        Require-Value $evidence 'contractAvailable' 'True'
        Require-Value $evidence 'unityAvailable' 'True'
    }
    foreach ($mirror in @($mirror1, $mirror2)) {
        Require-Value $mirror 'enabledAtActivation' 'True'
        Require-Value $mirror 'shortcutAtActivation' 'F9 + LeftShift'
        Require-Value $mirror 'verboseAtCleanup' 'True'
        Require-Value $mirror 'patchedResult' '12'
        Require-Value $mirror 'cleanupResult' '2'
    }
    Require-Value $mirror1 'verboseAtActivation' 'False'
    Require-Value $mirror2 'verboseAtActivation' 'True'
    foreach ($guide in @($guide1, $guide2)) {
        Require-Value $guide 'showPanelAtActivation' 'False'
        Require-Value $guide 'shortcutAtCleanup' 'F8'
        Require-Value $guide 'currentAtCleanup' 'next phase'
        Require-Value $guide 'legacyAtActivation' 'legacy'
    }
    Require-Value $guide1 'shortcutBeforeMutation' 'Not set'
    Require-Value $guide1 'currentBeforeMutation' 'current'
    Require-Value $guide2 'shortcutBeforeMutation' 'F8'
    Require-Value $guide2 'currentBeforeMutation' 'next phase'

    $threadValues = @(
        $mirror1.activateThread, $mirror1.pollThread, $mirror1.cleanupThread,
        $mirror2.activateThread, $mirror2.pollThread, $mirror2.cleanupThread,
        $guide1.activateThread, $guide1.pollThread, $guide1.cleanupThread,
        $guide2.activateThread, $guide2.pollThread, $guide2.cleanupThread
    ) | Sort-Object -Unique
    if ($threadValues.Count -ne 1) {
        throw 'Milestone 3 callbacks did not remain on one Unity thread.'
    }

    $mirrorText = [IO.File]::ReadAllText($mirrorConfig)
    $guideText = [IO.File]::ReadAllText($guideConfig)
    foreach ($text in @(
            'Enabled = true',
            'Verbose = true',
            'Shortcut = F9 + LeftShift'
        )) {
        if (-not $mirrorText.Contains($text)) {
            throw "Mirror persisted configuration is missing: $text"
        }
    }
    foreach ($text in @(
            'Show Panel = false',
            'Toggle Shortcut = F8',
            'Current = next phase',
            'Legacy = legacy'
        )) {
        if (-not $guideText.Contains($text)) {
            throw "Guide persisted configuration is missing: $text"
        }
    }
    if ($mirrorText.Contains('Show Panel') -or
        $guideText.Contains('Diagnostics')) {
        throw 'Milestone 3 consumer configuration files collided.'
    }

    foreach ($run in 1..2) {
        $runDirectory = Join-Path $resultPath "RUN-$run"
        $log = [IO.File]::ReadAllText((
            Join-Path $runDirectory 'DSPPluginManager.log'
        ))
        foreach ($message in @(
                'Plugin activation acknowledged: identifier=fixture.rm34.guide version=1.0.0',
                'Plugin activation acknowledged: identifier=fixture.rm34.mirror version=1.0.0',
                'Plugin activation failed: identifier=fixture.rm20.activation-failure version=1.0.0',
                'Plugin cleanup acknowledged: identifier=fixture.rm34.guide version=1.0.0 state=Stopped.',
                'Plugin cleanup acknowledged: identifier=fixture.rm34.mirror version=1.0.0 state=Stopped.',
                'Orderly plugin shutdown completed; closing current-run log.'
            )) {
            if (-not $log.Contains($message)) {
                throw "Milestone 3 run $run log is missing: $message"
            }
        }
        if (($log -split '\r?\n' | Where-Object {
                $_ -match 'Plugin activation acknowledged: identifier=fixture\.rm34\.'
            }).Count -ne 2) {
            throw "Milestone 3 run $run activation count was invalid."
        }
        $checkpoint = [IO.File]::ReadAllText((
            Join-Path $runDirectory 'bootstrap-checkpoint.txt'
        ))
        if ($checkpoint -notmatch
            "event=unity-main-thread-handoff \| thread=$($threadValues[0]) \|") {
            throw "Milestone 3 run $run handoff thread was invalid."
        }
    }

    [IO.File]::WriteAllLines(
        (Join-Path $resultPath 'RUN-RESULT.txt'),
        @(
            "Installed executable: $gameExecutable",
            "Run 1 process: $($runs[0].ProcessId)",
            "Run 2 process: $($runs[1].ProcessId)",
            'Responsive windows observed: 2',
            "Unity main thread: $($threadValues[0])",
            'Mirror activations/polls/cleanups: 2/2/2',
            'Guide activations/polls/cleanups: 2/2/2',
            'Isolated activation failures: 2',
            'Extra-key Mirror shortcut rejection: True',
            'Persisted Mirror shortcut across restart: F9 + LeftShift',
            'Persisted Guide shortcut across restart: F8',
            'Persisted Mirror Boolean across restart: True',
            'Persisted Guide Boolean across restart: False',
            'Guide late current/legacy retention: True',
            'Guide explicit save/reopen value: next phase',
            'Cross-plugin configuration collision: False',
            'BepInEx assemblies loaded: 0',
            'Manager-owned Harmony patch/cleanup: True',
            "Manager SHA-256: $((Get-FileHash -LiteralPath (Join-Path $managerPath 'DSPPluginManager.dll') -Algorithm SHA256).Hash)",
            "Contracts SHA-256: $((Get-FileHash -LiteralPath (Join-Path $managerPath 'DSPPluginManager.Contracts.dll') -Algorithm SHA256).Hash)",
            "Unity host SHA-256: $((Get-FileHash -LiteralPath (Join-Path $managerPath 'DSPPluginManager.UnityHost.dll') -Algorithm SHA256).Hash)",
            "Mirror fixture SHA-256: $((Get-FileHash -LiteralPath $mirrorFixture -Algorithm SHA256).Hash)",
            "Guide fixture SHA-256: $((Get-FileHash -LiteralPath $guideFixture -Algorithm SHA256).Hash)",
            "Proxy SHA-256: $bundleProxyHash"
        ),
        (New-Object Text.UTF8Encoding($false))
    )
}
finally {
    Stop-InstalledGame
    if ((Test-Path -LiteralPath $managerPath -PathType Container) -and
        -not (Test-Path -LiteralPath (
            Join-Path $resultPath 'RUN-RESULT.txt'
        ) -PathType Leaf)) {
        $failedState = Join-Path $resultPath 'FAILED-INSTALLED-STATE'
        New-Item -ItemType Directory -Force -Path $failedState | Out-Null
        foreach ($relative in @(
                'bootstrap-checkpoint.txt',
                'logs',
                'config',
                'writable'
            )) {
            $source = Join-Path $managerPath $relative
            if (Test-Path -LiteralPath $source) {
                Copy-Item -LiteralPath $source -Destination $failedState `
                    -Recurse -Force
            }
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
    foreach ($protectedFile in $protectedFiles) {
        if ((Get-FileHash -LiteralPath $protectedFile `
                -Algorithm SHA256).Hash -cne $beforeHashes[$protectedFile]) {
            throw "Protected game file changed during Milestone 3: $protectedFile"
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
    if ($newEmergencyFiles.Count -ne 0) {
        throw 'Milestone 3 produced a bootstrap emergency record.'
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
Write-Output "Milestone 3 installed check passed: $resultPath"
