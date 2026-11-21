<#
.SYNOPSIS
  Fails when contracts/src, contracts/schemas or contracts/typecheck changed against a base ref
  without a greater version in contracts/package.json.

.DESCRIPTION
  The npm package is published from main when contracts/ changes. Publishing the same version
  twice is rejected by GitHub Packages, so a contract change has to bump the version.

  A base ref with no contracts/package.json is the initial package and any version is accepted.
#>
[CmdletBinding()]
param(
    [string] $BaseRef = 'origin/main'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root

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

$baseJson = git show "${BaseRef}:contracts/package.json" 2>$null
if (-not $baseJson) {
    Write-Host "No contracts package on $BaseRef; $headVersion is the initial version."
    exit 0
}

$baseVersion = Get-PackageVersion ($baseJson -join "`n")
$changed = @(git diff --name-only $BaseRef -- contracts/src contracts/schemas contracts/typecheck)
if ($changed.Count -eq 0) {
    Write-Host "Contract outputs match $BaseRef (version $headVersion)."
    exit 0
}

if ($headVersion -eq $baseVersion) {
    Write-Host "Contracts changed but version is still $headVersion. Bump contracts/package.json."
    $changed | ForEach-Object { Write-Host "  $_" }
    exit 1
}

if ((ConvertTo-SemVer $headVersion) -le (ConvertTo-SemVer $baseVersion)) {
    Write-Host "Version $headVersion is not greater than $baseVersion."
    exit 1
}

Write-Host "Contract version $baseVersion -> $headVersion."
exit 0
