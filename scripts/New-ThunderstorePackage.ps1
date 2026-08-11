[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DllPath,

    [Parameter(Mandatory = $true)]
    [string]$BuildInfoPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$VersionNumber,

    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [string]$OutputDirectory = (
        Join-Path $RepositoryRoot 'artifacts\packages'
    )
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$manifestTemplatePath = Join-Path $RepositoryRoot `
    'packaging\manifest.template.json'
$readmePath = Join-Path $RepositoryRoot 'packaging\README.md'
$licensePath = Join-Path $RepositoryRoot 'LICENSE'

foreach ($requiredPath in @(
        $DllPath,
        $BuildInfoPath,
        $manifestTemplatePath,
        $readmePath,
        $licensePath
    )) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required package input was not found: $requiredPath"
    }
}

$template = Get-Content -Raw -LiteralPath $manifestTemplatePath
$placeholder = '{{VERSION_NUMBER}}'
if (([regex]::Matches(
            $template,
            [regex]::Escape($placeholder)
        )).Count -ne 1) {
    throw "Manifest template must contain exactly one $placeholder placeholder."
}
$manifestText = $template.Replace($placeholder, $VersionNumber)
$manifest = $manifestText | ConvertFrom-Json
if ($manifest.version_number -cne $VersionNumber) {
    throw 'Manifest version replacement failed.'
}

$iconStream = New-Object System.IO.MemoryStream
$bitmap = New-Object System.Drawing.Bitmap 256, 256
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
try {
    $graphics.Clear([System.Drawing.Color]::FromArgb(22, 30, 46))
    $accent = New-Object System.Drawing.SolidBrush (
        [System.Drawing.Color]::FromArgb(70, 190, 210)
    )
    $detail = New-Object System.Drawing.SolidBrush (
        [System.Drawing.Color]::FromArgb(236, 196, 72)
    )
    try {
        $graphics.FillRectangle($accent, 36, 36, 184, 64)
        $graphics.FillRectangle($accent, 36, 156, 184, 64)
        $graphics.FillRectangle($detail, 96, 96, 64, 64)
    }
    finally {
        $detail.Dispose()
        $accent.Dispose()
    }
    $bitmap.Save(
        $iconStream,
        [System.Drawing.Imaging.ImageFormat]::Png
    )
}
finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}
$iconBytes = $iconStream.ToArray()
$iconStream.Dispose()

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$packagePath = Join-Path $OutputDirectory `
    "DSPPluginManager-$VersionNumber.zip"
if (Test-Path -LiteralPath $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}

$archive = [System.IO.Compression.ZipFile]::Open(
    $packagePath,
    [System.IO.Compression.ZipArchiveMode]::Create
)
try {
    $manifestEntry = $archive.CreateEntry(
        'manifest.json',
        [System.IO.Compression.CompressionLevel]::Optimal
    )
    $manifestStream = $manifestEntry.Open()
    try {
        $writer = New-Object System.IO.StreamWriter(
            $manifestStream,
            (New-Object System.Text.UTF8Encoding($false))
        )
        try {
            $writer.Write($manifestText)
        }
        finally {
            $writer.Dispose()
        }
    }
    finally {
        $manifestStream.Dispose()
    }

    $iconEntry = $archive.CreateEntry(
        'icon.png',
        [System.IO.Compression.CompressionLevel]::Optimal
    )
    $packagedIconStream = $iconEntry.Open()
    try {
        $packagedIconStream.Write($iconBytes, 0, $iconBytes.Length)
    }
    finally {
        $packagedIconStream.Dispose()
    }

    foreach ($entry in @(
            @($readmePath, 'README.md'),
            @($licensePath, 'LICENSE'),
            @($BuildInfoPath, 'BUILD-INFO.txt'),
            @($DllPath, 'DSPPluginManager.dll')
        )) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $entry[0],
            $entry[1],
            [System.IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
finally {
    $archive.Dispose()
}

Write-Output $packagePath
