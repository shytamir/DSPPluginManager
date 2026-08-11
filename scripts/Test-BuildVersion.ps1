[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DllPath,

    [Parameter(Mandatory = $true)]
    [string]$BuildInfoPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$ExpectedPackageVersion,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$ExpectedSemanticVersion,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+\.0$')]
    [string]$ExpectedAssemblyVersion,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+\.[0-9a-f]{7}$')]
    [string]$ExpectedReleaseLabel,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{7,40}$')]
    [string]$ExpectedCommit,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$ExpectedSequence
)

$ErrorActionPreference = 'Stop'
foreach ($requiredPath in @($DllPath, $BuildInfoPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required build output was not found: $requiredPath"
    }
}

if ((Get-Item -LiteralPath $DllPath).Length -ne 0) {
    throw 'The bootstrap artifact must remain an empty placeholder DLL.'
}
if ($ExpectedSemanticVersion -cne $ExpectedPackageVersion) {
    throw 'Semantic and package versions must match.'
}
if ($ExpectedAssemblyVersion -cne "$ExpectedPackageVersion.0") {
    throw 'Assembly version does not follow the M.m.N.0 contract.'
}
$expectedShortCommit = $ExpectedCommit.Substring(0, 7).ToLowerInvariant()
if ($ExpectedReleaseLabel -cne "$ExpectedPackageVersion.$expectedShortCommit") {
    throw 'Release label does not follow the M.m.N.commit contract.'
}

$expectedLines = @(
    "Release label: $ExpectedReleaseLabel",
    "Package version: $ExpectedPackageVersion",
    "Semantic version: $ExpectedSemanticVersion",
    "Assembly version: $ExpectedAssemblyVersion",
    "Source commit: $($ExpectedCommit.ToLowerInvariant())",
    "Workflow sequence: $ExpectedSequence",
    'Artifact status: placeholder'
)
$actualLines = @(Get-Content -LiteralPath $BuildInfoPath)
if ($actualLines.Count -ne $expectedLines.Count) {
    throw "BUILD-INFO contains $($actualLines.Count) lines; expected $($expectedLines.Count)."
}
for ($index = 0; $index -lt $expectedLines.Count; $index++) {
    if ($actualLines[$index] -cne $expectedLines[$index]) {
        throw "BUILD-INFO line $($index + 1) is invalid."
    }
}

Write-Output "Build version validation passed: $ExpectedReleaseLabel"
