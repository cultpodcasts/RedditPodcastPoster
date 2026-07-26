#Requires -Version 7
<#
  afterFileEdit: if the edited file is a test source, record it against this
  conversation's state so the stop hook only follow-ups the agent that edited it
  (never other agents sharing the same workspace / dirty git tree).
#>
$ErrorActionPreference = 'Stop'
$stdin = [Console]::In.ReadToEnd()
$payload = $null
try { $payload = $stdin | ConvertFrom-Json } catch { }

$filePath = $null
if ($payload) {
    foreach ($name in @('file_path', 'filePath', 'path')) {
        if ($payload.PSObject.Properties.Name -contains $name -and $payload.$name) {
            $filePath = [string]$payload.$name
            break
        }
    }
}

if (-not $filePath) {
    Write-Output '{}'
    exit 0
}

# Without a conversation id we cannot isolate agents — skip recording (fail open).
$conversationId = $null
if ($payload) {
    foreach ($name in @('conversation_id', 'conversationId', 'session_id', 'sessionId')) {
        if ($payload.PSObject.Properties.Name -contains $name -and $payload.$name) {
            $conversationId = [string]$payload.$name
            break
        }
    }
}
if ([string]::IsNullOrWhiteSpace($conversationId)) {
    Write-Output '{}'
    exit 0
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

$script = Join-Path $repoRoot 'scripts\assert-unit-test-guardrails.ps1'
# Probe whether this path is a test source the scanner cares about.
$out = & pwsh -NoProfile -File $script -Path $filePath -Json 2>&1 | Out-String
$result = $null
try { $result = $out | ConvertFrom-Json } catch { }

# Non-test paths return ok:true with scanned:[]. Only track files the scanner accepted.
$scanned = @()
if ($result -and $result.scanned) { $scanned = @($result.scanned) }
if ($scanned.Count -eq 0) {
    Write-Output '{}'
    exit 0
}

$violations = @()
if ($result -and -not $result.ok -and $result.violations) {
    $violations = @($result.violations)
}

$stateDir = Join-Path $PSScriptRoot '.state'
New-Item -ItemType Directory -Force -Path $stateDir | Out-Null
# Sanitize conversation id for a filename (UUIDs are fine; strip path separators just in case).
$safeId = ($conversationId -replace '[\\/:*?"<>|]', '_')
$stateFile = Join-Path $stateDir "unit-test-guardrail-$safeId.json"

$state = [pscustomobject]@{
    conversation_id = $conversationId
    files           = @()
    last_violations = @()
    updated_at      = [DateTime]::UtcNow.ToString('o')
}
if (Test-Path -LiteralPath $stateFile) {
    try {
        $loaded = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
        if ($loaded) {
            $state.files = @($loaded.files)
            if ($loaded.last_violations) { $state.last_violations = @($loaded.last_violations) }
        }
    }
    catch { }
}

$fileSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($f in @($state.files)) {
    if ($f) { [void]$fileSet.Add([string]$f) }
}
[void]$fileSet.Add($filePath)
$state.files = @($fileSet)
$state.updated_at = [DateTime]::UtcNow.ToString('o')

# Keep latest violation snapshot for this conversation (overwrite stale rows for this file).
$kept = [System.Collections.Generic.List[object]]::new()
foreach ($v in @($state.last_violations)) {
    if (-not $v) { continue }
    $vFile = [string]$v.file
    # Drop prior rows that refer to this absolute path or its repo-relative form.
    if ($vFile -and (
            [string]::Equals($vFile, $filePath, [StringComparison]::OrdinalIgnoreCase) -or
            $filePath.EndsWith($vFile, [StringComparison]::OrdinalIgnoreCase))) {
        continue
    }
    $kept.Add($v) | Out-Null
}
foreach ($v in $violations) { $kept.Add($v) | Out-Null }
$state.last_violations = @($kept)

($state | ConvertTo-Json -Compress -Depth 8) | Set-Content -LiteralPath $stateFile -Encoding utf8

Write-Output '{}'
exit 0
