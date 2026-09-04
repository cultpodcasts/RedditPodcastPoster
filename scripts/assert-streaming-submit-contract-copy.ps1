<#
.SYNOPSIS
  Fails unless RPP docs/contracts/streaming-submit-contract.json matches Api JSON fixture.

.DESCRIPTION
  Api/tests/fixtures/streaming-submit-contract.json is canonical for cross-repo JSON.
  Skips (exit 0) when the sibling Api repo is not present.

  Usage (from RedditPodcastPoster git root):
    pwsh ./scripts/assert-streaming-submit-contract-copy.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$rppRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$copyPath = Join-Path $rppRoot 'docs\contracts\streaming-submit-contract.json'
$apiPath = Join-Path $rppRoot '..\..\Api\tests\fixtures\streaming-submit-contract.json'
# Workspace layout: cultpodcasts/RedditPodcastPoster → ../../Api when Api is sibling of cultpodcasts
# Also try ../Api when Api is sibling of RedditPodcastPoster parent (website-style)
$apiAlt = Join-Path $rppRoot '..\..\..\Api\tests\fixtures\streaming-submit-contract.json'
# Common local: C:\Users\...\source\repos\Api and ...\cultpodcasts\RedditPodcastPoster
$apiReposSibling = Join-Path $rppRoot '..\..\Api\tests\fixtures\streaming-submit-contract.json'

$candidates = @($apiPath, $apiAlt, $apiReposSibling) | Select-Object -Unique
$resolvedApi = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not (Test-Path -LiteralPath $copyPath)) {
    Write-Host "Missing RPP copy: $copyPath"
    exit 1
}

if (-not $resolvedApi) {
    Write-Host "Sibling Api JSON fixture not found; skip streaming-submit contract copy check."
    exit 0
}

$left = Get-FileHash -LiteralPath $copyPath -Algorithm SHA256
$right = Get-FileHash -LiteralPath $resolvedApi -Algorithm SHA256
if ($left.Hash -ne $right.Hash) {
    Write-Host 'streaming-submit-contract.json copies differ. Copy from Api/tests/fixtures/streaming-submit-contract.json'
    Write-Host " RPP: $($left.Hash)"
    Write-Host " Api: $($right.Hash)"
    Write-Host " Api path: $resolvedApi"
    exit 1
}

Write-Host "streaming-submit-contract.json copies match ($($left.Hash.Substring(0, 12))…)."
