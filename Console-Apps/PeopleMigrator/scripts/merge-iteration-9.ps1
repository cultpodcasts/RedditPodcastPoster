# One-off merge: people-seed.iteration-8.json -> people-seed.iteration-9.json
# Local seed only — no Cosmos writes.

$ErrorActionPreference = 'Stop'
$migrator = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$inputPath = Join-Path $migrator 'people-seed.iteration-8.json'
$outputPath = Join-Path $migrator 'people-seed.iteration-9.json'

Add-Type -AssemblyName System.Text.Json

function Normalize-Handle([string]$h) {
    if ([string]::IsNullOrWhiteSpace($h)) { return $null }
    $t = $h.Trim()
    if (-not $t.StartsWith('@')) { $t = "@$t" }
    return $t.ToLowerInvariant()
}

function As-StringArray($value) {
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return @($value | ForEach-Object { "$_" }) }
    return @("$value")
}

function Union-Strings([string[]]$values) {
    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $out = [System.Collections.Generic.List[string]]::new()
    foreach ($x in $values) {
        if ([string]::IsNullOrWhiteSpace($x)) { continue }
        $t = $x.Trim()
        if ($seen.Add($t)) { $out.Add($t) }
    }
    return $out.ToArray()
}

function Union-EpisodeIds($a, $b) {
    return (Union-Strings (As-StringArray $a) + (As-StringArray $b) | Sort-Object)
}

function Merge-Notes([string[]]$values) {
    $parts = Union-Strings $values
    if ($parts.Count -eq 0) { return $null }
    return ($parts -join '; ')
}

function Row-Score($row) {
    $ep = (As-StringArray $row.sourceEpisodeIds).Count
    $handles = (@($row.twitterHandle, $row.blueskyHandle) | Where-Object { $_ }).Count
    return ($ep * 10) + $handles
}

function Merge-TwoRows($survivor, $duplicate, [string]$canonicalName, [string[]]$extraAliases) {
    $name = if ($canonicalName) { $canonicalName } else { $survivor.name }
    $twitter = $survivor.twitterHandle
    $bluesky = $survivor.blueskyHandle
    $notes = @($survivor.notes, $duplicate.notes)

    if ($duplicate.twitterHandle) {
        if (-not $twitter) {
            $twitter = $duplicate.twitterHandle
        }
        elseif ((Normalize-Handle $duplicate.twitterHandle) -ne (Normalize-Handle $twitter)) {
            $notes += "also twitter: $($duplicate.twitterHandle)"
        }
    }

    if ($duplicate.blueskyHandle) {
        if (-not $bluesky) {
            $bluesky = $duplicate.blueskyHandle
        }
        elseif ((Normalize-Handle $duplicate.blueskyHandle) -ne (Normalize-Handle $bluesky)) {
            $notes += "also bluesky: $($duplicate.blueskyHandle)"
        }
    }

    $aliases = Union-Strings @(
        (As-StringArray $survivor.aliases)
        (As-StringArray $duplicate.aliases)
        $duplicate.name
        $extraAliases
    ) | Where-Object {
        $_ -and ($_ -ne $name) -and
        (Normalize-Handle $_) -ne (Normalize-Handle $twitter) -and
        (Normalize-Handle $_) -ne (Normalize-Handle $bluesky)
    }

    return [PSCustomObject]@{
        name = $name
        aliases = @($aliases)
        twitterHandle = $twitter
        blueskyHandle = $bluesky
        sourceEpisodeIds = @(Union-EpisodeIds $survivor.sourceEpisodeIds $duplicate.sourceEpisodeIds)
        notes = Merge-Notes $notes
    }
}

function Merge-Indices($people, [int[]]$indices, [string]$canonicalName, [string[]]$extraAliases, [string]$label, [int]$forceSurvivorIndex = -1) {
    if ($indices.Count -lt 2) {
        return @{ Merged = $false; Reason = 'not enough rows'; Label = $label }
    }

    $rows = $indices | ForEach-Object { @{ Index = $_; Row = $people[$_] } }
    if ($forceSurvivorIndex -ge 0) {
        $survivorItem = $rows | Where-Object { $_.Index -eq $forceSurvivorIndex } | Select-Object -First 1
        $others = $rows | Where-Object { $_.Index -ne $forceSurvivorIndex }
    }
    else {
        $ordered = $rows | Sort-Object { Row-Score $_.Row } -Descending
        $survivorItem = $ordered[0]
        $others = $ordered[1..($ordered.Count - 1)]
    }

    $merged = $survivorItem.Row
    foreach ($item in $others) {
        $merged = Merge-TwoRows $merged $item.Row $canonicalName $extraAliases
    }

    $people[$survivorItem.Index] = $merged
    $remove = @($others | ForEach-Object { $_.Index }) | Sort-Object -Descending
    foreach ($idx in $remove) {
        $people.RemoveAt($idx)
    }

    return @{
        Merged = $true
        Label = $label
        SurvivorName = $merged.name
        Twitter = $merged.twitterHandle
        Bluesky = $merged.blueskyHandle
        EpisodeCount = (As-StringArray $merged.sourceEpisodeIds).Count
        Removed = $remove.Count
        BeforeRows = $indices.Count
    }
}

function Find-IndexByHandle($people, [string]$twitter, [string]$bluesky) {
    for ($i = 0; $i -lt $people.Count; $i++) {
        $row = $people[$i]
        if ($twitter -and (Normalize-Handle $row.twitterHandle) -eq (Normalize-Handle $twitter)) { return $i }
        if ($bluesky -and (Normalize-Handle $row.blueskyHandle) -eq (Normalize-Handle $bluesky)) { return $i }
    }
    return -1
}

function Find-IndicesByName($people, [string]$name) {
    $indices = [System.Collections.Generic.List[int]]::new()
    for ($i = 0; $i -lt $people.Count; $i++) {
        if ($people[$i].name -eq $name) { $indices.Add($i) }
    }
    return $indices
}

$jsonText = [System.IO.File]::ReadAllText($inputPath)
$json = $jsonText | ConvertFrom-Json
$people = [System.Collections.Generic.List[object]]::new()
$people.AddRange(@($json.people))
$startCount = $people.Count
$results = [System.Collections.Generic.List[object]]::new()
$skipped = [System.Collections.Generic.List[object]]::new()

$phase1 = @(
    @{ Name = 'Ali Fortescue'; Handles = @('@AliFortescue', '@alifortescuenews.bsky.social') }
    @{ Name = 'Dave Aronberg'; Handles = @('@aronberg', '@davearonberg.bsky.social') }
    @{ Name = 'Ilhan Omar'; Handles = @('@Ilhan', '@repilhan.bsky.social') }
    @{ Name = 'Jared Moskowitz'; Handles = @('@repmoskowitz.bsky.social', '@JaredEMoskowitz') }
    @{ Name = 'Lauren Boebert'; Handles = @('@laurenboebert', '@RepBoebert') }
    @{ Name = 'Luba Kassova'; Handles = @('@LubaKassova', '@lubakas.bsky.social') }
    @{ Name = 'Pontsho Pilane'; Handles = @('@pontsho_pilane', '@pontsho.bsky.social') }
    @{ Name = 'Robert Garcia'; Handles = @('@RepRobertGarcia', '@RobertGarcia') }
    @{ Name = 'Tom Swarbrick'; Handles = @('@TomSwarbrick1', '@tomswarbrick.bsky.social') }
)

foreach ($pair in $phase1) {
    $indices = [System.Collections.Generic.List[int]]::new()
    foreach ($h in $pair.Handles) {
        if ($h -match '\.bsky\.social$|\.house\.gov$') {
            $idx = Find-IndexByHandle $people $null $h
        }
        else {
            $idx = Find-IndexByHandle $people $h $null
        }
        if ($idx -ge 0 -and $indices -notcontains $idx) { $indices.Add($idx) }
    }
    if ($indices.Count -lt 2) {
        $indices = Find-IndicesByName $people $pair.Name
    }
    if ($indices.Count -lt 2) {
        $skipped.Add([PSCustomObject]@{ Phase = 1; Pair = $pair.Name; Reason = 'rows not found' })
        continue
    }
    $r = Merge-Indices $people @($indices) $pair.Name @() "Phase1: $($pair.Name)"
    if ($r.Merged) { $results.Add([PSCustomObject]$r) }
}

$phase2 = @(
    @{ PersonName = 'Anderson Cooper'; ShowName = 'Anderson Cooper 360°'; PersonTwitter = '@andersoncooper'; ShowTwitter = '@AC360' }
    @{ PersonName = 'Ari Melber'; ShowName = 'The Beat with Ari Melber'; PersonTwitter = '@AriMelber'; ShowTwitter = '@TheBeatWithAri' }
    @{ PersonName = 'Liz Kendall'; ShowName = 'Liz Kendall MP'; PersonTwitter = '@leicesterliz'; ShowBluesky = '@lizforleicester.bsky.social' }
)

foreach ($pair in $phase2) {
    $personIdx = Find-IndexByHandle $people $pair.PersonTwitter $null
    if ($personIdx -lt 0) { $personIdx = (Find-IndicesByName $people $pair.PersonName | Select-Object -First 1) }

    $showIdx = -1
    if ($pair.ShowTwitter) { $showIdx = Find-IndexByHandle $people $pair.ShowTwitter $null }
    if ($showIdx -lt 0 -and $pair.ShowBluesky) { $showIdx = Find-IndexByHandle $people $null $pair.ShowBluesky }
    if ($showIdx -lt 0) { $showIdx = (Find-IndicesByName $people $pair.ShowName | Select-Object -First 1) }

    if ($personIdx -lt 0 -or $showIdx -lt 0 -or $personIdx -eq $showIdx) {
        $skipped.Add([PSCustomObject]@{
            Phase = 2
            Pair = "$($pair.ShowName) -> $($pair.PersonName)"
            Reason = if ($personIdx -lt 0 -or $showIdx -lt 0) { 'row missing' } else { 'same row' }
        })
        continue
    }

    $r = Merge-Indices $people @($personIdx, $showIdx) $pair.PersonName @($pair.ShowName) "Phase2: $($pair.ShowName) -> $($pair.PersonName)" $personIdx
    if ($r.Merged) { $results.Add([PSCustomObject]$r) }
}

$json.people = @($people | Sort-Object { $_.name } -Culture 'en-US')
$json.generatedAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffZ')

# Build output with System.Text.Json for stable UTF-8 + array formatting
$peopleEntries = foreach ($p in $json.people) {
    $entry = [ordered]@{
        name = $p.name
        aliases = @(As-StringArray $p.aliases)
        sourceEpisodeIds = @(As-StringArray $p.sourceEpisodeIds)
    }
    if ($p.twitterHandle) { $entry.twitterHandle = $p.twitterHandle }
    if ($p.blueskyHandle) { $entry.blueskyHandle = $p.blueskyHandle }
    if ($p.notes) { $entry.notes = $p.notes }
    $entry
}

$document = [ordered]@{
    generatedAt = $json.generatedAt
    sourceCache = $json.sourceCache
    sourceBackupPath = $json.sourceBackupPath
    people = $peopleEntries
}

$options = [System.Text.Json.JsonSerializerOptions]::new()
$options.PropertyNamingPolicy = [System.Text.Json.JsonNamingPolicy]::CamelCase
$options.DefaultIgnoreCondition = [System.Text.Json.Serialization.JsonIgnoreCondition]::WhenWritingNull
$options.WriteIndented = $true

$outputJson = [System.Text.Json.JsonSerializer]::Serialize($document, $options)
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($outputPath, $outputJson, $utf8NoBom)

Write-Host "Input:  $startCount people"
Write-Host "Output: $($peopleEntries.Count) people"
Write-Host "Wrote:  $outputPath"
Write-Host ''
Write-Host '=== Merged ==='
$results | Format-Table -AutoSize Label, SurvivorName, Twitter, Bluesky, EpisodeCount, BeforeRows, Removed
if ($skipped.Count -gt 0) {
    Write-Host '=== Skipped ==='
    $skipped | Format-Table -AutoSize
}
