<#
.SYNOPSIS
  Checks the hand-written contracts/package.json version.

.DESCRIPTION
  Generated TypeScript, Zod and JSON Schema are gitignored, so this script does not
  diff contracts/src, contracts/schemas or contracts/typecheck.

  The published version is the semver in package.json, bumped by hand when the wire
  shape changes. GitHub Packages rejects a second publish of the same version, which
  is what stops an unchanged version shipping again.

  When the inputs that define that shape differ from the base ref, the version must
  be greater than the base. Those inputs are the exporter, the export attributes,
  the HTTP JSON options, and Cloud/Api/Dtos. A domain enum that an exported DTO
  references can live outside that list; that change still needs a manual bump.
#>
[CmdletBinding()]
param(
    [string] $BaseRef = 'origin/main'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root

# Source of the wire shape. Not the generated package files.
$ContractInputs = @(
    'Tools/ContractExporter'
    'Cloud/Api/Contracts'
    'Cloud/Api/Serialization/ApiJson.cs'
    'Cloud/Api/Dtos'
)

function Get-PackageVersion([string] $json) {
    return ([System.Text.Json.JsonDocument]::Parse($json)).RootElement.GetProperty('version').GetString()
}

function ConvertTo-SemVer([string] $version) {
    $core = ($version -split '-', 2)[0]
    $parts = $core.Split('.')
    if ($parts.Length -lt 2) {
        throw "Not a semver: $version"
    }
    $patch = 0
    if ($parts.Length -gt 2) {
        $patch = [int]$parts[2]
    }
    return [version]::new([int]$parts[0], [int]$parts[1], $patch)
}

$headJson = Get-Content -LiteralPath (Join-Path $root 'contracts/package.json') -Raw
$headVersion = Get-PackageVersion $headJson
if ([string]::IsNullOrWhiteSpace($headVersion)) {
    Write-Host 'contracts/package.json has no version.'
    exit 1
}
$null = ConvertTo-SemVer $headVersion

$baseJson = git show "${BaseRef}:contracts/package.json" 2>$null
if (-not $baseJson) {
    Write-Host "No contracts package on $BaseRef; $headVersion is the initial version."
    exit 0
}

$baseVersion = Get-PackageVersion ($baseJson -join "`n")
$changed = @(git diff --name-only $BaseRef -- $ContractInputs)
# Tests do not change the published shape.
$changed = @($changed | Where-Object { $_ -notlike 'Tools/ContractExporter.Tests/*' })

if ($changed.Count -eq 0) {
    Write-Host "Contract inputs match $BaseRef (version $headVersion)."
    exit 0
}

if ($headVersion -eq $baseVersion) {
    Write-Host "Contract inputs changed but version is still $headVersion. Bump contracts/package.json."
    $changed | ForEach-Object { Write-Host "  $_" }
    exit 1
}

if ((ConvertTo-SemVer $headVersion) -le (ConvertTo-SemVer $baseVersion)) {
    Write-Host "Version $headVersion is not greater than $baseVersion."
    exit 1
}

Write-Host "Contract version $baseVersion -> $headVersion."
exit 0
