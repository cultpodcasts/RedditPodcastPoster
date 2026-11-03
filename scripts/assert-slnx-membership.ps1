<#
.SYNOPSIS
  Every on-disk *.csproj is either in RedditPodcastPoster.slnx or explicitly allowlisted.

.DESCRIPTION
  Closes the silent-omit class of bug from #1145: test / library projects that exist on disk
  but never run under `dotnet test RedditPodcastPoster.slnx`.

  Allowlist (documented exclusions — keep in sync with films-refactor/README.md):
    - films-refactor/**  — throwaway GATE 1 wire rewrite; must not be in the slnx
    - **/obj/**, **/bin/** — generated WorkerExtensions etc.
    - .tmp*/** — local scratch (gitignored)

  Usage (from RedditPodcastPoster git root):
    pwsh ./scripts/assert-slnx-membership.ps1
#>
[CmdletBinding()]
param(
    [string] $SolutionPath = 'RedditPodcastPoster.slnx'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$slnxPath = Join-Path $root $SolutionPath
if (-not (Test-Path -LiteralPath $slnxPath)) {
    Write-Error "Solution not found: $slnxPath"
    exit 1
}

$slnx = Get-Content -LiteralPath $slnxPath -Raw
$inSlnx = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($m in [regex]::Matches($slnx, 'Path="([^"]+\.csproj)"')) {
    [void]$inSlnx.Add(($m.Groups[1].Value -replace '\\', '/'))
}

function Test-IsAllowlisted([string] $rel) {
    $n = $rel -replace '\\', '/'
    if ($n -match '(^|/)\.tmp|/obj/|/bin/') { return $true }
    if ($n.StartsWith('films-refactor/', [StringComparison]::OrdinalIgnoreCase)) { return $true }
    return $false
}

$missing = [System.Collections.Generic.List[string]]::new()
Get-ChildItem -LiteralPath $root -Recurse -Filter *.csproj -File |
    ForEach-Object {
        $rel = $_.FullName.Substring($root.Path.Length).TrimStart('\', '/').Replace('\', '/')
        if (Test-IsAllowlisted $rel) { return }
        if (-not $inSlnx.Contains($rel)) {
            $missing.Add($rel)
        }
    }

if ($missing.Count -gt 0) {
    Write-Host 'csproj on disk but missing from solution (and not allowlisted):'
    $missing | Sort-Object | ForEach-Object { Write-Host "  $_" }
    Write-Host 'Add to RedditPodcastPoster.slnx, or extend the allowlist in assert-slnx-membership.ps1 with a documented reason.'
    exit 1
}

Write-Host "slnx membership OK ($($inSlnx.Count) projects; allowlisted exclusions only)."
exit 0
