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
if ([string]::IsNullOrWhiteSpace($LockPath)) {
    $LockPath = Join-Path $RepositoryRoot `
        'dependencies\managed-dependencies.lock.json'
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $RepositoryRoot 'artifacts\managed-dependencies'
}

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-PublicKeyToken {
    param([Parameter(Mandatory = $true)][Reflection.AssemblyName]$AssemblyName)

    $bytes = $AssemblyName.GetPublicKeyToken()
    if ($null -eq $bytes -or $bytes.Length -eq 0) {
        return 'null'
    }

    return [BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
}

function Get-CultureName {
    param([Parameter(Mandatory = $true)][Reflection.AssemblyName]$AssemblyName)

    if ([string]::IsNullOrWhiteSpace($AssemblyName.CultureInfo.Name)) {
        return 'neutral'
    }

    return $AssemblyName.CultureInfo.Name
}

function Get-ZipEntry {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Compression.ZipArchive]$Archive,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $matches = @($Archive.Entries | Where-Object FullName -CEQ $Path)
    if ($matches.Count -ne 1) {
        throw "Expected one case-sensitive package entry '$Path'; found $($matches.Count)."
    }

    return $matches[0]
}

function Read-ZipText {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Compression.ZipArchiveEntry]$Entry
    )

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

function Copy-ZipEntry {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Compression.ZipArchiveEntry]$Entry,

        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    $source = $Entry.Open()
    try {
        $target = [System.IO.File]::Create($Destination)
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

$repositoryPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$lockFile = (Resolve-Path -LiteralPath $LockPath).Path
$lock = [System.IO.File]::ReadAllText(
    $lockFile,
    (New-Object System.Text.UTF8Encoding($false, $true))
) | ConvertFrom-Json

if ($lock.schemaVersion -ne 1) {
    throw "Unsupported managed dependency lock schema: $($lock.schemaVersion)"
}
if ($lock.targetFramework -cne '.NETFramework3.5' -or
    $lock.assetFramework -cne 'net35') {
    throw 'The managed dependency lock must select the reviewed net35 asset group.'
}

$packages = @($lock.packages)
if ($packages.Count -ne 4) {
    throw "The managed runtime closure must contain exactly four packages; found $($packages.Count)."
}
$packageIds = @($packages | ForEach-Object { $_.id })
if (($packageIds | Select-Object -Unique).Count -ne $packageIds.Count) {
    throw 'The managed dependency lock contains duplicate package identifiers.'
}
$assetNames = @($packages | ForEach-Object { $_.asset.fileName })
if (($assetNames | Select-Object -Unique).Count -ne $assetNames.Count) {
    throw 'The managed dependency lock contains duplicate runtime file names.'
}

$artifactsRoot = Join-Path $repositoryPath 'artifacts'
$outputPath = [IO.Path]::GetFullPath($OutputRoot)
if (-not $outputPath.StartsWith(
        [IO.Path]::GetFullPath($artifactsRoot).TrimEnd('\') + '\',
        [StringComparison]::OrdinalIgnoreCase
    )) {
    throw 'Managed dependency output must remain under the repository artifacts directory.'
}

$packageDirectory = Join-Path $outputPath 'packages'
$runtimeDirectory = Join-Path $outputPath 'runtime'
$referenceDirectory = Join-Path $outputPath 'plugin-references'
$noticeDirectory = Join-Path $outputPath 'notices'
New-Item -ItemType Directory -Force -Path $packageDirectory | Out-Null
Reset-GeneratedDirectory -Path $runtimeDirectory -AllowedParent $outputPath
Reset-GeneratedDirectory -Path $referenceDirectory -AllowedParent $outputPath
Reset-GeneratedDirectory -Path $noticeDirectory -AllowedParent $outputPath

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$packageById = @{}
foreach ($package in $packages) {
    $packageById[$package.id] = $package
}
foreach ($package in $packages) {
    $packagePath = Join-Path $packageDirectory $package.packageFile
    $validCachedPackage = $false
    if (Test-Path -LiteralPath $packagePath -PathType Leaf) {
        $validCachedPackage = (
            (Get-Item -LiteralPath $packagePath).Length -eq $package.packageLength -and
            (Get-Sha256 -Path $packagePath) -ceq $package.packageSha256
        )
    }

    if (-not $validCachedPackage) {
        $downloadPath = $packagePath + '.download'
        if (Test-Path -LiteralPath $downloadPath) {
            Remove-Item -LiteralPath $downloadPath -Force
        }
        try {
            Invoke-WebRequest -UseBasicParsing -Uri $package.sourceUrl `
                -OutFile $downloadPath
            if ((Get-Item -LiteralPath $downloadPath).Length -ne
                $package.packageLength) {
                throw "Package length mismatch for $($package.id) $($package.version)."
            }
            if ((Get-Sha256 -Path $downloadPath) -cne $package.packageSha256) {
                throw "Package SHA-256 mismatch for $($package.id) $($package.version)."
            }
            Move-Item -LiteralPath $downloadPath -Destination $packagePath -Force
        }
        finally {
            if (Test-Path -LiteralPath $downloadPath) {
                Remove-Item -LiteralPath $downloadPath -Force
            }
        }
    }

    if ((Get-Item -LiteralPath $packagePath).Length -ne $package.packageLength -or
        (Get-Sha256 -Path $packagePath) -cne $package.packageSha256) {
        throw "Cached package integrity failed for $($package.id) $($package.version)."
    }

    $archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        [xml]$nuspec = Read-ZipText -Entry (
            Get-ZipEntry -Archive $archive -Path $package.nuspecPath
        )
        $namespace = New-Object Xml.XmlNamespaceManager($nuspec.NameTable)
        $namespace.AddNamespace(
            'n',
            'http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd'
        )
        $metadataPath = '/n:package/n:metadata'
        $actualId = $nuspec.SelectSingleNode("$metadataPath/n:id", $namespace).InnerText
        $actualVersion = $nuspec.SelectSingleNode(
            "$metadataPath/n:version",
            $namespace
        ).InnerText
        if ($actualId -cne $package.id -or $actualVersion -cne $package.version) {
            throw "NuGet identity mismatch for $($package.packageFile)."
        }

        if ($package.licenseType -ceq 'url') {
            $licenseNode = $nuspec.SelectSingleNode(
                "$metadataPath/n:licenseUrl",
                $namespace
            )
            $licenseValue = $licenseNode.InnerText
        }
        else {
            $licenseNode = $nuspec.SelectSingleNode(
                "$metadataPath/n:license",
                $namespace
            )
            $licenseValue = $licenseNode.InnerText
            if ($licenseNode.GetAttribute('type') -cne $package.licenseType) {
                throw "NuGet license type mismatch for $($package.id)."
            }
        }
        if ($licenseValue -cne $package.licenseValue) {
            throw "NuGet license metadata mismatch for $($package.id)."
        }

        $groupPath = "$metadataPath/n:dependencies/n:group[@targetFramework='$($lock.targetFramework)']"
        $dependencyGroup = $nuspec.SelectSingleNode($groupPath, $namespace)
        if ($null -eq $dependencyGroup) {
            throw "Missing $($lock.targetFramework) dependency group for $($package.id)."
        }
        $actualDependencies = @($dependencyGroup.SelectNodes('n:dependency', $namespace))
        $expectedDependencies = @($package.dependencies)
        if ($actualDependencies.Count -ne $expectedDependencies.Count) {
            throw "Dependency count mismatch for $($package.id) $($lock.targetFramework)."
        }
        foreach ($expected in $expectedDependencies) {
            $matches = @($actualDependencies | Where-Object {
                    $_.GetAttribute('id') -ceq $expected.id -and
                    $_.GetAttribute('version') -ceq $expected.declaredVersion
                })
            if ($matches.Count -ne 1) {
                throw "Dependency declaration mismatch for $($package.id) -> $($expected.id)."
            }
            if (-not $packageById.ContainsKey($expected.id)) {
                throw "Dependency closure is missing $($expected.id)."
            }
            $selected = $packageById[$expected.id]
            if ($selected.version -cne $expected.selectedVersion -or
                [Version]$selected.version -lt [Version]$expected.declaredVersion) {
                throw "Selected dependency does not satisfy $($package.id) -> $($expected.id)."
            }
        }

        $assetEntry = Get-ZipEntry -Archive $archive -Path $package.asset.packagePath
        if ($assetEntry.Length -ne $package.asset.length) {
            throw "Asset length mismatch for $($package.asset.fileName)."
        }
        Copy-ZipEntry -Entry $assetEntry -Destination (
            Join-Path $runtimeDirectory $package.asset.fileName
        )
    }
    finally {
        $archive.Dispose()
    }
}

$managedAssemblyNames = @($packages | ForEach-Object { $_.asset.assemblyName })
foreach ($package in $packages) {
    $asset = $package.asset
    $runtimePath = Join-Path $runtimeDirectory $asset.fileName
    if ((Get-Item -LiteralPath $runtimePath).Length -ne $asset.length -or
        (Get-Sha256 -Path $runtimePath) -cne $asset.sha256) {
        throw "Runtime asset integrity mismatch for $($asset.fileName)."
    }

    $identity = [Reflection.AssemblyName]::GetAssemblyName($runtimePath)
    if ($identity.Name -cne $asset.assemblyName -or
        $identity.Version.ToString() -cne $asset.assemblyVersion -or
        (Get-CultureName -AssemblyName $identity) -cne $asset.culture -or
        (Get-PublicKeyToken -AssemblyName $identity) -cne $asset.publicKeyToken) {
        throw "Assembly identity mismatch for $($asset.fileName)."
    }

    $assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom($runtimePath)
    $actualReferences = @($assembly.GetReferencedAssemblies() | Where-Object {
            $managedAssemblyNames -ccontains $_.Name
        })
    $expectedReferences = @($asset.managedReferences)
    if ($actualReferences.Count -ne $expectedReferences.Count) {
        throw "Managed reference count mismatch for $($asset.fileName)."
    }
    foreach ($expectedReference in $expectedReferences) {
        $matches = @($actualReferences | Where-Object {
                $_.Name -ceq $expectedReference.name -and
                $_.Version.ToString() -ceq $expectedReference.version -and
                (Get-PublicKeyToken -AssemblyName $_) -ceq
                    $expectedReference.publicKeyToken
            })
        if ($matches.Count -ne 1) {
            throw "Managed reference mismatch for $($asset.fileName) -> $($expectedReference.name)."
        }
    }

    if ($asset.pluginReference) {
        Copy-Item -LiteralPath $runtimePath -Destination $referenceDirectory
    }
}

$runtimeFiles = @(Get-ChildItem -LiteralPath $runtimeDirectory -File)
if ($runtimeFiles.Count -ne 4) {
    throw "Runtime staging must contain exactly four assemblies; found $($runtimeFiles.Count)."
}
$referenceFiles = @(Get-ChildItem -LiteralPath $referenceDirectory -File)
if ($referenceFiles.Count -ne 1 -or
    $referenceFiles[0].Name -cne '0Harmony.dll') {
    throw 'The plugin reference set must expose only the reviewed 0Harmony contract.'
}

$sourceNoticeDirectory = Join-Path $repositoryPath 'dependencies\notices'
foreach ($notice in @($lock.notices)) {
    $sourcePath = Join-Path $sourceNoticeDirectory $notice.file
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Required third-party notice is missing: $($notice.file)"
    }
    if ((Get-Sha256 -Path $sourcePath) -cne $notice.sha256) {
        throw "Third-party notice hash mismatch: $($notice.file)"
    }
    Copy-Item -LiteralPath $sourcePath -Destination $noticeDirectory
}
if (@(Get-ChildItem -LiteralPath $noticeDirectory -File).Count -ne 4) {
    throw 'Notice staging must contain exactly four reviewed license files.'
}

$reportPath = Join-Path $outputPath 'DEPENDENCY-REPORT.md'
$lockHash = (Get-Sha256 -Path $lockFile).ToLowerInvariant()
$packageRows = $packages | ForEach-Object {
    "| $($_.id) | $($_.version) | $($_.asset.fileName) | ``$($_.asset.assemblyVersion)`` | ``$($_.asset.sha256.ToLowerInvariant())`` |"
}
$report = @"
# Managed dependency verification

| Property | Value |
| --- | --- |
| Lock schema | $($lock.schemaVersion) |
| Target asset group | ``$($lock.assetFramework)`` ($($lock.targetFramework)) |
| Runtime closure | Four assemblies |
| Plugin compile references | ``0Harmony.dll`` only |
| Required notices | Four verified files |
| Lock SHA-256 | ``$lockHash`` |

| Package | Version | Runtime assembly | Assembly version | DLL SHA-256 |
| --- | --- | --- | --- | --- |
$($packageRows -join [Environment]::NewLine)
"@
Set-Content -LiteralPath $reportPath -Value $report -Encoding utf8

Write-Output "Managed dependency validation passed: $runtimeDirectory"
