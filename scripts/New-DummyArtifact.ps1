[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
[System.IO.File]::WriteAllBytes($OutputPath, [byte[]]@())

$artifact = Get-Item -LiteralPath $OutputPath
if ($artifact.Length -ne 0) {
    throw "Placeholder DLL is not empty: $OutputPath"
}

Write-Output $artifact.FullName
