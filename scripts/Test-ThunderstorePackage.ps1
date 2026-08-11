[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$ExpectedVersion,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedDllPath,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedBuildInfoPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+\.0$')]
    [string]$ExpectedAssemblyVersion,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+\.[0-9a-f]{7}$')]
    [string]$ExpectedReleaseLabel,

    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [string]$ReportPath = (
        Join-Path $RepositoryRoot 'artifacts\PACKAGE-REPORT.md'
    )
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Read-ZipText {
    param([System.IO.Compression.ZipArchiveEntry]$Entry)

    $stream = $Entry.Open()
    try {
        $reader = New-Object System.IO.StreamReader(
            $stream,
            (New-Object System.Text.UTF8Encoding($false, $true)),
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

function Get-ZipEntryHash {
    param([System.IO.Compression.ZipArchiveEntry]$Entry)

    $stream = $Entry.Open()
    try {
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            return [BitConverter]::ToString(
                $sha256.ComputeHash($stream)
            ).Replace('-', '')
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

foreach ($requiredPath in @(
        $PackagePath,
        $ExpectedDllPath,
        $ExpectedBuildInfoPath
    )) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required validation input was not found: $requiredPath"
    }
}

$maximumPackageBytes = 5242880000
$packageLength = (Get-Item -LiteralPath $PackagePath).Length
if ($packageLength -gt $maximumPackageBytes) {
    throw "Package exceeds Thunderstore's documented size limit."
}

$requiredRootEntries = @('manifest.json', 'README.md', 'icon.png')
$expectedPayloadEntries = @(
    'LICENSE',
    'BUILD-INFO.txt',
    'DSPPluginManager.dll'
)
$archive = [System.IO.Compression.ZipFile]::OpenRead(
    (Resolve-Path -LiteralPath $PackagePath)
)
try {
    $files = @($archive.Entries | Where-Object {
            -not $_.FullName.EndsWith('/')
        })
    $entryNames = @($files | ForEach-Object { $_.FullName })

    if (@($entryNames | Where-Object { $_.Contains('\') }).Count -gt 0) {
        throw 'Package contains non-portable backslash entry names.'
    }
    if (($entryNames | Select-Object -Unique).Count -ne $entryNames.Count) {
        throw 'Package contains duplicate file entries.'
    }
    foreach ($requiredEntry in $requiredRootEntries + $expectedPayloadEntries) {
        if ($entryNames -cnotcontains $requiredEntry) {
            throw "Package entry is missing or incorrectly cased: $requiredEntry"
        }
    }
    $expectedEntries = @($requiredRootEntries + $expectedPayloadEntries)
    $unexpectedEntries = @($entryNames | Where-Object {
            $expectedEntries -cnotcontains $_
        })
    if ($unexpectedEntries.Count -gt 0) {
        throw "Package contains unexpected entries: $($unexpectedEntries -join ', ')"
    }

    $manifestEntry = $files |
        Where-Object FullName -CEQ 'manifest.json' |
        Select-Object -First 1
    $manifest = (Read-ZipText -Entry $manifestEntry) | ConvertFrom-Json
    $manifestFields = @($manifest.PSObject.Properties.Name)
    foreach ($requiredField in @(
            'name',
            'version_number',
            'website_url',
            'description',
            'dependencies'
        )) {
        if ($manifestFields -cnotcontains $requiredField) {
            throw "Manifest is missing required field: $requiredField"
        }
    }
    if ($manifest.name -notmatch '^[A-Za-z0-9_]{1,128}$') {
        throw 'Manifest name violates Thunderstore character or length rules.'
    }
    if ($manifest.version_number -cne $ExpectedVersion -or
        $manifest.version_number -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') {
        throw 'Manifest version is not the expected three-part semantic version.'
    }
    if ([string]::IsNullOrWhiteSpace($manifest.description) -or
        $manifest.description.Length -gt 250) {
        throw 'Manifest description must contain 1 to 250 characters.'
    }
    if ($null -eq $manifest.dependencies) {
        throw 'Manifest dependencies must be a JSON array.'
    }
    foreach ($dependency in @($manifest.dependencies)) {
        if ($dependency -isnot [string] -or
            $dependency -notmatch '^.+-.+-(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') {
            throw "Manifest dependency is invalid: $dependency"
        }
    }
    if ($manifest.website_url -isnot [string]) {
        throw 'Manifest website_url must be a string.'
    }
    if ($manifest.website_url.Length -gt 0) {
        $website = $null
        if (-not [Uri]::TryCreate(
                $manifest.website_url,
                [UriKind]::Absolute,
                [ref]$website
            ) -or $website.Scheme -notin @('http', 'https')) {
            throw 'Manifest website_url must be empty or an absolute HTTP(S) URL.'
        }
    }

    $readmeEntry = $files |
        Where-Object FullName -CEQ 'README.md' |
        Select-Object -First 1
    $readme = Read-ZipText -Entry $readmeEntry
    if ([string]::IsNullOrWhiteSpace($readme)) {
        throw 'Package README must be non-empty UTF-8 Markdown.'
    }

    $iconEntry = $files |
        Where-Object FullName -CEQ 'icon.png' |
        Select-Object -First 1
    $iconStream = $iconEntry.Open()
    try {
        $icon = [System.Drawing.Image]::FromStream($iconStream)
        try {
            if ($icon.Width -ne 256 -or $icon.Height -ne 256 -or
                $icon.RawFormat.Guid -ne
                [System.Drawing.Imaging.ImageFormat]::Png.Guid) {
                throw 'Package icon must be a 256 by 256 PNG.'
            }
        }
        finally {
            $icon.Dispose()
        }
    }
    finally {
        $iconStream.Dispose()
    }

    $dllEntry = $files |
        Where-Object FullName -CEQ 'DSPPluginManager.dll' |
        Select-Object -First 1
    if ($dllEntry.Length -le 0 -or
        (Get-Item -LiteralPath $ExpectedDllPath).Length -le 0) {
        throw 'The compiled product assembly must be non-empty.'
    }
    $expectedDllHash = (
        Get-FileHash -LiteralPath $ExpectedDllPath -Algorithm SHA256
    ).Hash
    if ((Get-ZipEntryHash -Entry $dllEntry) -cne $expectedDllHash) {
        throw 'Packaged product DLL does not match the build artifact.'
    }

    $buildInfoEntry = $files |
        Where-Object FullName -CEQ 'BUILD-INFO.txt' |
        Select-Object -First 1
    $packagedBuildInfo = Read-ZipText -Entry $buildInfoEntry
    $expectedBuildInfo = [System.IO.File]::ReadAllText(
        (Resolve-Path -LiteralPath $ExpectedBuildInfoPath),
        (New-Object System.Text.UTF8Encoding($false, $true))
    )
    if ($packagedBuildInfo -cne $expectedBuildInfo) {
        throw 'Packaged BUILD-INFO does not match the generated version record.'
    }
    foreach ($expectedText in @(
            "Release label: $ExpectedReleaseLabel",
            "Package version: $ExpectedVersion",
            "Semantic version: $ExpectedVersion",
            "Assembly version: $ExpectedAssemblyVersion",
            'Artifact status: compiled foundation'
        )) {
        if (-not $packagedBuildInfo.Contains($expectedText)) {
            throw "Packaged BUILD-INFO is missing: $expectedText"
        }
    }
}
finally {
    $archive.Dispose()
}

$packageHash = (
    Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256
).Hash.ToLowerInvariant()
New-Item -ItemType Directory -Force `
    -Path (Split-Path -Parent $ReportPath) | Out-Null
$report = @"
# Thunderstore package verification

| Check | Result |
| --- | --- |
| Package version | ``$ExpectedVersion`` |
| Declared assembly version | ``$ExpectedAssemblyVersion`` |
| Diagnostic release label | ``$ExpectedReleaseLabel`` |
| Required root files | Passed |
| Manifest v1 fields | Passed |
| UTF-8 README | Passed |
| 256 by 256 PNG icon | Passed |
| Compiled product integrity | Passed |
| Package size | $packageLength bytes |
| SHA-256 | ``$packageHash`` |
"@
Set-Content -LiteralPath $ReportPath -Value $report -Encoding utf8

Write-Output "Thunderstore package validation passed: $ExpectedVersion"
