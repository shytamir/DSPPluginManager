[CmdletBinding()]
param(
    [string]$RepositoryRoot = '',

    [string]$LockPath = '',

    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$repositoryPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
if ([string]::IsNullOrWhiteSpace($LockPath)) {
    $LockPath = Join-Path $repositoryPath `
        'dependencies\unitydoorstop.lock.json'
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryPath 'artifacts\unitydoorstop'
}

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Reset-GeneratedDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$AllowedParent
    )

    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $fullParent = [IO.Path]::GetFullPath($AllowedParent).TrimEnd('\')
    if (-not $fullPath.StartsWith(
            $fullParent + '\',
            [StringComparison]::OrdinalIgnoreCase
        )) {
        throw "Refusing to reset generated directory outside '$fullParent': $fullPath"
    }
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $fullPath | Out-Null
}

function Copy-ZipEntry {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Compression.ZipArchiveEntry]$Entry,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    $source = $Entry.Open()
    try {
        $target = [IO.File]::Create($Destination)
        try {
            $source.CopyTo($target)
        }
        finally {
            $target.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
}

function Get-PeMachine {
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = [IO.File]::OpenRead($Path)
    try {
        $reader = New-Object IO.BinaryReader($stream)
        try {
            if ($reader.ReadUInt16() -ne 0x5A4D) {
                throw "Native proxy is not a PE image: $Path"
            }
            $stream.Position = 0x3C
            $peOffset = $reader.ReadInt32()
            $stream.Position = $peOffset
            if ($reader.ReadUInt32() -ne 0x00004550) {
                throw "Native proxy has no PE signature: $Path"
            }
            return ('0x{0:X4}' -f $reader.ReadUInt16())
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

$lockFile = (Resolve-Path -LiteralPath $LockPath).Path
$lock = [IO.File]::ReadAllText(
    $lockFile,
    (New-Object Text.UTF8Encoding($false, $true))
) | ConvertFrom-Json
if ($lock.schemaVersion -ne 1 -or $lock.name -cne 'UnityDoorstop') {
    throw 'Unsupported UnityDoorstop lock.'
}
if ($lock.version -cne '3.4.0.0' -or
    $lock.tag -cne 'v3.4.0.0' -or
    $lock.architecture -cne 'x64' -or
    $lock.peMachine -cne '0x8664') {
    throw 'UnityDoorstop must remain pinned to the reviewed 3.4.0.0 x64 build.'
}
if ($lock.sourceRepository -cne
        'https://github.com/NeighTools/UnityDoorstop' -or
    $lock.sourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'UnityDoorstop source provenance is incomplete or unexpected.'
}
$expectedUrl = $lock.sourceRepository + '/releases/download/' +
    $lock.tag + '/' + $lock.releaseAsset.fileName
if ($lock.releaseAsset.sourceUrl -cne $expectedUrl -or
    $lock.releaseAsset.fileName -cne 'Doorstop_x64_3.4.0.0.zip') {
    throw 'UnityDoorstop release source does not match the reviewed tag and asset.'
}

$artifactsRoot = Join-Path $repositoryPath 'artifacts'
$outputPath = [IO.Path]::GetFullPath($OutputRoot)
if (-not $outputPath.StartsWith(
        [IO.Path]::GetFullPath($artifactsRoot).TrimEnd('\') + '\',
        [StringComparison]::OrdinalIgnoreCase
    )) {
    throw 'UnityDoorstop output must remain under repository artifacts.'
}
$packageDirectory = Join-Path $outputPath 'packages'
$runtimeDirectory = Join-Path $outputPath 'runtime'
$noticeDirectory = Join-Path $outputPath 'notices'
New-Item -ItemType Directory -Force -Path $packageDirectory | Out-Null
Reset-GeneratedDirectory -Path $runtimeDirectory -AllowedParent $outputPath
Reset-GeneratedDirectory -Path $noticeDirectory -AllowedParent $outputPath

$archivePath = Join-Path $packageDirectory $lock.releaseAsset.fileName
$validArchive = Test-Path -LiteralPath $archivePath -PathType Leaf
if ($validArchive) {
    $validArchive = (Get-Item -LiteralPath $archivePath).Length -eq
        $lock.releaseAsset.length -and
        (Get-Sha256 -Path $archivePath) -ceq $lock.releaseAsset.sha256
}
if (-not $validArchive) {
    $downloadPath = $archivePath + '.download'
    if (Test-Path -LiteralPath $downloadPath) {
        Remove-Item -LiteralPath $downloadPath -Force
    }
    try {
        [Net.ServicePointManager]::SecurityProtocol =
            [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -Uri $lock.releaseAsset.sourceUrl `
            -OutFile $downloadPath
        if ((Get-Item -LiteralPath $downloadPath).Length -ne
                $lock.releaseAsset.length -or
            (Get-Sha256 -Path $downloadPath) -cne
                $lock.releaseAsset.sha256) {
            throw 'Downloaded UnityDoorstop archive failed integrity validation.'
        }
        Move-Item -LiteralPath $downloadPath -Destination $archivePath -Force
    }
    finally {
        if (Test-Path -LiteralPath $downloadPath) {
            Remove-Item -LiteralPath $downloadPath -Force
        }
    }
}

$archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $entries = @($archive.Entries | Where-Object {
            $_.FullName -ceq $lock.proxy.archivePath
        })
    if ($entries.Count -ne 1) {
        throw "Expected one '$($lock.proxy.archivePath)' release entry."
    }
    if ($entries[0].Length -ne $lock.proxy.length) {
        throw 'UnityDoorstop proxy length does not match the lock.'
    }
    $proxyPath = Join-Path $runtimeDirectory $lock.proxy.fileName
    Copy-ZipEntry -Entry $entries[0] -Destination $proxyPath
}
finally {
    $archive.Dispose()
}

if ((Get-Sha256 -Path $proxyPath) -cne $lock.proxy.sha256) {
    throw 'UnityDoorstop proxy SHA-256 does not match the lock.'
}
if ((Get-PeMachine -Path $proxyPath) -cne $lock.peMachine) {
    throw 'UnityDoorstop proxy is not the pinned x64 PE image.'
}
$fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo(
    $proxyPath
).FileVersion
if ($fileVersion -cne $lock.version) {
    throw "UnityDoorstop file version mismatch: $fileVersion"
}

$sourceNotice = Join-Path $repositoryPath (
    'dependencies\notices\' + $lock.notice.file
)
if (-not (Test-Path -LiteralPath $sourceNotice -PathType Leaf) -or
    (Get-Sha256 -Path $sourceNotice) -cne $lock.notice.sha256) {
    throw 'The reviewed UnityDoorstop CC0 notice is missing or changed.'
}
Copy-Item -LiteralPath $sourceNotice -Destination $noticeDirectory

$reportPath = Join-Path $outputPath 'UNITYDOORSTOP-REPORT.md'
$report = @"
# UnityDoorstop verification

| Property | Value |
| --- | --- |
| Version | ``$($lock.version)`` |
| Architecture / PE machine | ``$($lock.architecture)`` / ``$($lock.peMachine)`` |
| Source | $($lock.sourceRepository) at ``$($lock.sourceCommit)`` (``$($lock.tag)``) |
| Release archive SHA-256 | ``$($lock.releaseAsset.sha256.ToLowerInvariant())`` |
| Proxy SHA-256 | ``$($lock.proxy.sha256.ToLowerInvariant())`` |
| License | CC0 1.0 Universal; reviewed notice verified |
"@
Set-Content -LiteralPath $reportPath -Value $report -Encoding utf8

Write-Output "UnityDoorstop validation passed: $proxyPath"
