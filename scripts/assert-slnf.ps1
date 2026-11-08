<#
.SYNOPSIS
  Every project listed in each *.slnf exists on disk and is a member of the referenced solution.

.DESCRIPTION
  Folded from #1021: Discover.slnf previously referenced RedditPodcastPoster.Common (removed)
  and used Windows backslash paths that the slnx parser rejects on Linux.

  Usage (from RedditPodcastPoster git root):
    pwsh ./scripts/assert-slnf.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$failures = [System.Collections.Generic.List[string]]::new()

$filters = @(Get-ChildItem -LiteralPath $root -Filter *.slnf -File)
if ($filters.Count -eq 0) {
    Write-Host 'No *.slnf files; nothing to validate.'
    exit 0
}

function Test-HasNoteProperty {
    param(
        [Parameter(Mandatory)] $Object,
        [Parameter(Mandatory)] [string] $Name
    )
    return $null -ne $Object.PSObject.Properties[$Name]
}

foreach ($filterFile in $filters) {
    $json = Get-Content -LiteralPath $filterFile.FullName -Raw | ConvertFrom-Json

    if (-not (Test-HasNoteProperty $json 'solution') -or $null -eq $json.solution) {
        $failures.Add("$($filterFile.Name): missing solution object")
        continue
    }

    $solution = $json.solution
    if (-not (Test-HasNoteProperty $solution 'path') -or [string]::IsNullOrWhiteSpace([string]$solution.path)) {
        $failures.Add("$($filterFile.Name): missing or blank solution.path")
        continue
    }
    if (-not (Test-HasNoteProperty $solution 'projects') -or $null -eq $solution.projects) {
        $failures.Add("$($filterFile.Name): missing or null solution.projects")
        continue
    }

    $solutionRel = ($solution.path -replace '\\', '/')
    $solutionPath = Join-Path $root $solutionRel
    if (-not (Test-Path -LiteralPath $solutionPath)) {
        $failures.Add("$($filterFile.Name): solution path missing: $solutionRel")
        continue
    }

    $slnx = Get-Content -LiteralPath $solutionPath -Raw
    $inSlnx = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($m in [regex]::Matches($slnx, 'Path="([^"]+\.csproj)"')) {
        [void]$inSlnx.Add(($m.Groups[1].Value -replace '\\', '/'))
    }

    foreach ($proj in @($solution.projects)) {
        if ($null -eq $proj -or [string]::IsNullOrWhiteSpace([string]$proj)) {
            $failures.Add("$($filterFile.Name): blank or null project entry in solution.projects")
            continue
        }
        $norm = ($proj -replace '\\', '/')
        if ($proj -match '\\') {
            $failures.Add(('{0}: use forward slashes (Linux slnx parser): {1}' -f $filterFile.Name, $proj))
        }
        $onDisk = Join-Path $root $norm
        if (-not (Test-Path -LiteralPath $onDisk)) {
            $failures.Add(('{0}: project missing on disk: {1}' -f $filterFile.Name, $norm))
        }
        if (-not $inSlnx.Contains($norm)) {
            $failures.Add(('{0}: project not in {1}: {2}' -f $filterFile.Name, $solutionRel, $norm))
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host '*.slnf validation failed:'
    $failures | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "slnf OK ($($filters.Count) filter(s))."
exit 0
