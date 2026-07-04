#Requires -Version 7.0
<#
.SYNOPSIS
  Runs episode-domain coverage tests and enforces per-file baselines from coverage-baseline.json.

.DESCRIPTION
  Collects Cobertura from four test projects, merges coverage by source file (union of hits),
  and fails if branch/line coverage drops below documented baselines.
#>
param(
    [string]$BaselinePath = (Join-Path $PSScriptRoot '../plans/episode-domain-refactor/coverage-baseline.json'),
    [string]$ResultsDirectory = (Join-Path $PSScriptRoot '../TestResults/coverage-gate'),
    [switch]$SkipTest,
    [switch]$ReportOnly,
    [switch]$MeasureOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Normalize-SourceKey {
    param([string]$Path)
    $normalized = ($Path -replace '\\', '/').TrimStart('/')
    if ($normalized -notlike 'Class-Libraries/*') {
        $normalized = "Class-Libraries/$normalized"
    }
    return $normalized.ToLowerInvariant()
}

function To-Percent {
    param([int]$Covered, [int]$Total)
    if ($Total -le 0) { return 100.0 }
    return 100.0 * $Covered / $Total
}

function Merge-CoberturaReports {
    param([System.IO.FileInfo[]]$XmlFiles)

    $byFile = @{}

    foreach ($xmlFile in $XmlFiles) {
        [xml]$doc = Get-Content $xmlFile.FullName
        $classes = @($doc.SelectNodes('//class'))
        foreach ($class in $classes) {
            $key = Normalize-SourceKey $class.filename
            if (-not $byFile.ContainsKey($key)) {
                $byFile[$key] = @{
                    lineHits = @{}
                    branchLines = @{}
                }
            }

            $entry = $byFile[$key]

            foreach ($line in @($class.lines.line)) {
                $num = [int]$line.number
                if (-not $entry.lineHits.ContainsKey($num)) {
                    $entry.lineHits[$num] = 0
                }
                $entry.lineHits[$num] = [Math]::Max($entry.lineHits[$num], [int]$line.hits)

                if ($line.branch -eq 'True') {
                    if (-not $entry.branchLines.ContainsKey($num)) {
                        $entry.branchLines[$num] = @{ covered = 0; total = 0 }
                    }
                    $cond = $line.'condition-coverage'
                    if ($cond -match '\((\d+)/(\d+)\)') {
                        $c = [int]$Matches[1]
                        $t = [int]$Matches[2]
                        $entry.branchLines[$num].covered = [Math]::Max($entry.branchLines[$num].covered, $c)
                        $entry.branchLines[$num].total = [Math]::Max($entry.branchLines[$num].total, $t)
                    }
                }
            }

            foreach ($method in @($class.methods.method)) {
                foreach ($line in @($method.lines.line)) {
                    $num = [int]$line.number
                    if (-not $entry.lineHits.ContainsKey($num)) {
                        $entry.lineHits[$num] = 0
                    }
                    $entry.lineHits[$num] = [Math]::Max($entry.lineHits[$num], [int]$line.hits)

                    if ($line.branch -eq 'True') {
                        if (-not $entry.branchLines.ContainsKey($num)) {
                            $entry.branchLines[$num] = @{ covered = 0; total = 0 }
                        }
                        $cond = $line.'condition-coverage'
                        if ($cond -match '\((\d+)/(\d+)\)') {
                            $c = [int]$Matches[1]
                            $t = [int]$Matches[2]
                            $entry.branchLines[$num].covered = [Math]::Max($entry.branchLines[$num].covered, $c)
                            $entry.branchLines[$num].total = [Math]::Max($entry.branchLines[$num].total, $t)
                        }
                    }
                }
            }
        }
    }

    $result = @{}
    foreach ($key in $byFile.Keys) {
        $entry = $byFile[$key]
        $lineTotal = $entry.lineHits.Count
        $lineCovered = @($entry.lineHits.Values | Where-Object { $_ -gt 0 }).Count
        $branchTotal = 0
        $branchCovered = 0
        foreach ($branchLine in $entry.branchLines.Values) {
            $branchTotal += $branchLine.total
            $branchCovered += $branchLine.covered
        }

        $result[$key] = @{
            lineTotal = $lineTotal
            lineCovered = $lineCovered
            branchTotal = $branchTotal
            branchCovered = $branchCovered
        }
    }

    return $result
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot

try {
    $testProjects = @(
        'Class-Libraries/RedditPodcastPoster.Episodes.Tests/RedditPodcastPoster.Episodes.Tests.csproj',
        'Class-Libraries/RedditPodcastPoster.PodcastServices.Tests/RedditPodcastPoster.PodcastServices.Tests.csproj',
        'Class-Libraries/RedditPodcastPoster.UrlSubmission.Tests/RedditPodcastPoster.UrlSubmission.Tests.csproj',
        'Class-Libraries/RedditPodcastPoster.Persistence.Tests/RedditPodcastPoster.Persistence.Tests.csproj'
    )

    if (-not $SkipTest) {
        if (Test-Path $ResultsDirectory) {
            Remove-Item -Recurse -Force $ResultsDirectory
        }
        New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null

        foreach ($project in $testProjects) {
            Write-Host "Running coverage: $project"
            dotnet test $project `
                --configuration Release `
                --collect:'XPlat Code Coverage' `
                --results-directory $ResultsDirectory `
                -- `
                DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura `
                | Write-Host
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet test failed for $project (exit $LASTEXITCODE)"
            }
        }
    }

    $xmlFiles = Get-ChildItem -Path $ResultsDirectory -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction Stop
    if ($xmlFiles.Count -eq 0) {
        throw "No coverage.cobertura.xml files found under $ResultsDirectory"
    }

    $merged = Merge-CoberturaReports -XmlFiles $xmlFiles

    if ($MeasureOnly) {
        $patterns = @(
            @{ Group = 'episodes-domain'; Pattern = 'class-libraries/redditpodcastposter.episodes/matching/*'; Exclude = @('strategies') },
            @{ Group = 'episodes-domain'; Pattern = 'class-libraries/redditpodcastposter.episodes/merging/*'; Exclude = @('policies') },
            @{ Group = 'episodes-domain'; Pattern = 'class-libraries/redditpodcastposter.episodes/applying/*' },
            @{ Group = 'episodes-domain'; Pattern = 'class-libraries/redditpodcastposter.episodes/extensions/*' },
            @{ Group = 'episodes-domain'; Pattern = 'class-libraries/redditpodcastposter.episodes/episodereleasetolerance.cs' },
            @{ Group = 'episodes-domain'; Pattern = 'class-libraries/redditpodcastposter.episodes/matching/strategies/*' },
            @{ Group = 'episodes-domain'; Pattern = 'class-libraries/redditpodcastposter.episodes/merging/policies/*' },
            @{ Group = 'episodes-adapters'; Pattern = 'class-libraries/redditpodcastposter.episodes/adapters/*'; Exclude = @('inputs') },
            @{ Group = 'orchestration'; Pattern = 'class-libraries/redditpodcastposter.persistence/episodemerger.cs' },
            @{ Group = 'orchestration'; Pattern = 'class-libraries/redditpodcastposter.persistence/episodematcher.cs' },
            @{ Group = 'orchestration'; Pattern = 'class-libraries/redditpodcastposter.podcastservices/podcastupdater.cs' },
            @{ Group = 'orchestration'; Pattern = 'class-libraries/redditpodcastposter.urlsubmission/categoriseditemprocessor.cs' },
            @{ Group = 'orchestration'; Pattern = 'class-libraries/redditpodcastposter.urlsubmission/episodeenricher.cs' }
        )

        foreach ($spec in $patterns) {
            $files = $merged.Keys | Where-Object {
                $k = $_
                $match = ($k -like $spec.Pattern)
                if ($match -and ($spec.PSObject.Properties.Name -contains 'Exclude')) {
                    foreach ($ex in $spec.Exclude) {
                        if ($k -like "*$ex*") { return $false }
                    }
                }
                if ($match -and ($k -like '*iepisode*' -or $k -like '*/inputs/*')) { return $false }
                return $match
            } | Sort-Object

            foreach ($file in $files) {
                $stats = $merged[$file]
                $branchPct = [Math]::Round((To-Percent $stats.branchCovered $stats.branchTotal), 1)
                $linePct = [Math]::Round((To-Percent $stats.lineCovered $stats.lineTotal), 1)
                Write-Host ("{0}|{1}|branch={2}|line={3}|{4}/{5}|{6}/{7}" -f `
                    $spec.Group, ($file -replace '^class-libraries/', ''), $branchPct, $linePct, `
                    $stats.branchCovered, $stats.branchTotal, $stats.lineCovered, $stats.lineTotal)
            }
        }
        return
    }

    $baseline = Get-Content $BaselinePath -Raw | ConvertFrom-Json

    $failures = @()
    $reportRows = @()

    foreach ($entry in $baseline.files) {
        $key = Normalize-SourceKey $entry.path
        if (-not $merged.ContainsKey($key)) {
            $failures += "Missing coverage data for $($entry.path)"
            continue
        }

        $stats = $merged[$key]
        $branchPct = To-Percent $stats.branchCovered $stats.branchTotal
        $linePct = To-Percent $stats.lineCovered $stats.lineTotal

        $branchGate = if ($entry.PSObject.Properties.Name -contains 'minBranchCoverage') { [double]$entry.minBranchCoverage } else { [double]$entry.branchCoverage }
        $lineGate = if ($entry.PSObject.Properties.Name -contains 'minLineCoverage') { [double]$entry.minLineCoverage } else { [double]$entry.lineCoverage }

        $branchOk = ($stats.branchTotal -eq 0) -or ($branchPct + 0.0001 -ge $branchGate)
        $lineOk = ($stats.lineTotal -eq 0) -or ($linePct + 0.0001 -ge $lineGate)

        $status = if ($branchOk -and $lineOk) { 'PASS' } else { 'FAIL' }
        if ($status -eq 'FAIL' -and -not $ReportOnly) {
            if (-not $branchOk) {
                $failures += "$($entry.path): branch $([Math]::Round($branchPct, 1))% < baseline $branchGate% ($($stats.branchCovered)/$($stats.branchTotal))"
            }
            if (-not $lineOk) {
                $failures += "$($entry.path): line $([Math]::Round($linePct, 1))% < baseline $lineGate% ($($stats.lineCovered)/$($stats.lineTotal))"
            }
        }

        $reportRows += [PSCustomObject]@{
            Group = $entry.group
            Path = $entry.path
            Branch = [Math]::Round($branchPct, 1)
            BranchBaseline = $branchGate
            Line = [Math]::Round($linePct, 1)
            LineBaseline = $lineGate
            Status = $status
            Note = if ($entry.PSObject.Properties.Name -contains 'note') { $entry.note } else { $null }
        }
    }

    Write-Host ''
    Write-Host 'Episode domain coverage gate'
    Write-Host '============================='
    $reportRows | Sort-Object Group, Path | Format-Table -AutoSize | Out-String | Write-Host

    foreach ($group in $baseline.groups) {
        $groupFiles = @($baseline.files | Where-Object { $_.group -eq $group.name })
        $branchCovered = 0
        $branchTotal = 0
        $lineCovered = 0
        $lineTotal = 0
        foreach ($entry in $groupFiles) {
            $key = Normalize-SourceKey $entry.path
            if ($merged.ContainsKey($key)) {
                $stats = $merged[$key]
                $branchCovered += $stats.branchCovered
                $branchTotal += $stats.branchTotal
                $lineCovered += $stats.lineCovered
                $lineTotal += $stats.lineTotal
            }
        }

        $branchPct = To-Percent $branchCovered $branchTotal
        $linePct = To-Percent $lineCovered $lineTotal
        $branchGate = [double]$group.minBranchCoverage
        $lineGate = [double]$group.minLineCoverage
        $branchOk = ($branchTotal -eq 0) -or ($branchPct + 0.0001 -ge $branchGate)
        $lineOk = ($lineTotal -eq 0) -or ($linePct + 0.0001 -ge $lineGate)

        Write-Host ("Group {0}: branch {1:N1}% (baseline {2}%), line {3:N1}% (baseline {4}%)" -f `
            $group.name, $branchPct, $branchGate, $linePct, $lineGate)

        if (-not $ReportOnly) {
            if (-not $branchOk) {
                $failures += "Group $($group.name): branch $([Math]::Round($branchPct, 1))% < baseline $branchGate%"
            }
            if ($lineGate -gt 0 -and -not $lineOk) {
                $failures += "Group $($group.name): line $([Math]::Round($linePct, 1))% < baseline $lineGate%"
            }
        }
    }

    if ($failures.Count -gt 0 -and -not $ReportOnly) {
        Write-Host ''
        Write-Error ("Coverage gate failed:`n - " + ($failures -join "`n - "))
    }

    Write-Host ''
    Write-Host 'Coverage gate passed.'
}
finally {
    Pop-Location
}
