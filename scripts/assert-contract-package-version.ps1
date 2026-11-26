<#
.SYNOPSIS
  Checks that contracts/package.json names a release line, not a published patch.

.DESCRIPTION
  The publish workflow assigns the patch from GitHub Packages. Git stores major.minor.0
  only. A non-zero patch would look like a version that ships, and it does not.
  Edit the major or the minor for an intentional breaking or feature release line.
  There is no diff against a base ref: generated contracts are not in git, and a
  content hash at publish time decides whether a new patch is published.
#>
[CmdletBinding()]
param(
    [string] $PackageJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $PackageJson) {
    $root = Resolve-Path (Join-Path $PSScriptRoot '..')
    $PackageJson = Join-Path $root 'contracts/package.json'
}

$version = ([System.Text.Json.JsonDocument]::Parse((Get-Content -LiteralPath $PackageJson -Raw))).RootElement.GetProperty('version').GetString()
if ([string]::IsNullOrWhiteSpace($version)) {
    Write-Host 'contracts/package.json has no version.'
    exit 1
}

if ($version -notmatch '^\d+\.\d+\.0$') {
    Write-Host "contracts/package.json version must be major.minor.0 (got $version)."
    Write-Host 'The publish workflow assigns the patch. Change only the major or minor for an intentional release line.'
    exit 1
}

Write-Host "Contract release line $version. The published patch is assigned from the registry."
exit 0
