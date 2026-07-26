#Requires -Version 7
<#
  stop: if THIS conversation edited test files that still violate unit-test
  guardrails, emit followup_message so the same agent must fix them.

  Intentionally does NOT scan the whole git working tree (-GitChanged): that
  pulled other agents' dirty test files into this conversation's follow-up loop.
#>
$ErrorActionPreference = 'Stop'
$stdin = [Console]::In.ReadToEnd()
$payload = $null
try { $payload = $stdin | ConvertFrom-Json } catch { }

$status = if ($payload -and $payload.status) { [string]$payload.status } else { 'completed' }
$loopCount = 0
if ($payload -and ($payload.PSObject.Properties.Name -contains 'loop_count')) {
    $loopCount = [int]$payload.loop_count
}

if ($status -ne 'completed') {
    Write-Output '{}'
    exit 0
}

$conversationId = $null
if ($payload) {
    foreach ($name in @('conversation_id', 'conversationId', 'session_id', 'sessionId')) {
        if ($payload.PSObject.Properties.Name -contains $name -and $payload.$name) {
            $conversationId = [string]$payload.$name
            break
        }
    }
}

# No conversation scope ⇒ do not follow up (avoids cross-agent wake-ups).
if ([string]::IsNullOrWhiteSpace($conversationId)) {
    Write-Output '{}'
    exit 0
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

$stateDir = Join-Path $PSScriptRoot '.state'
$safeId = ($conversationId -replace '[\\/:*?"<>|]', '_')
$stateFile = Join-Path $stateDir "unit-test-guardrail-$safeId.json"

# One-time migration: drop the legacy shared jsonl so other agents stop inheriting it.
$legacyShared = Join-Path $stateDir 'unit-test-guardrail-failures.jsonl'
if (Test-Path -LiteralPath $legacyShared) {
    Remove-Item -LiteralPath $legacyShared -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path -LiteralPath $stateFile)) {
    Write-Output '{}'
    exit 0
}

$state = $null
try { $state = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json } catch { }
$files = @()
if ($state -and $state.files) { $files = @($state.files | Where-Object { $_ }) }

if ($files.Count -eq 0) {
    Remove-Item -LiteralPath $stateFile -Force -ErrorAction SilentlyContinue
    Write-Output '{}'
    exit 0
}

$existing = @($files | Where-Object { Test-Path -LiteralPath $_ })
if ($existing.Count -eq 0) {
    Remove-Item -LiteralPath $stateFile -Force -ErrorAction SilentlyContinue
    Write-Output '{}'
    exit 0
}

$script = Join-Path $repoRoot 'scripts\assert-unit-test-guardrails.ps1'
$jsonOut = & pwsh -NoProfile -File $script -Path $existing -Json 2>&1 | Out-String
$result = $null
try { $result = $jsonOut | ConvertFrom-Json } catch { }

$violations = @()
if ($result -and $result.violations) {
    $violations = @($result.violations)
}

# De-dupe by file:line:rule
$unique = [System.Collections.Generic.List[object]]::new()
$seen = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($v in $violations) {
    $key = '{0}:{1}:{2}' -f $v.file, $v.line, $v.rule
    if ($seen.Add($key)) { $unique.Add($v) | Out-Null }
}

if ($unique.Count -eq 0) {
    Remove-Item -LiteralPath $stateFile -Force -ErrorAction SilentlyContinue
    Write-Output '{}'
    exit 0
}

# Persist fresh violations for this conversation only (helps the next after-edit merge).
if ($state) {
    $state.last_violations = @($unique)
    $state.updated_at = [DateTime]::UtcNow.ToString('o')
    ($state | ConvertTo-Json -Compress -Depth 8) | Set-Content -LiteralPath $stateFile -Encoding utf8
}

$pathArgs = ($existing | ForEach-Object { "'$_'" }) -join ', '
$verifyCmd = "pwsh ./scripts/assert-unit-test-guardrails.ps1 -Path @($pathArgs)"

if ($loopCount -ge 3) {
    $summary = ($unique | Select-Object -First 8 | ForEach-Object {
            '{0}:{1} [{2}] {3}' -f $_.file, $_.line, $_.rule, $_.detail
        }) -join "`n"
    Write-Output (@{
            followup_message = @"
STOP: unit-test guardrail violations remain after $loopCount fix attempts in this conversation. Do not claim the test work is done.

$summary

Read .cursor/rules/unit-tests.mdc and fix every violation in the files this conversation edited, then re-run:
$verifyCmd
"@
        } | ConvertTo-Json -Compress)
    exit 0
}

$summary = ($unique | Select-Object -First 12 | ForEach-Object {
        '{0}:{1} [{2}] {3}' -f $_.file, $_.line, $_.rule, $_.detail
    }) -join "`n"

Write-Output (@{
        followup_message = @"
Unit-test guardrail violations detected in files this conversation edited (unit-tests.mdc). Fix them before finishing. Do not edit other agents' test files.

$summary

Re-read .cursor/rules/unit-tests.mdc. Prefer DomainTestFixture specimens, Fact(DisplayName=...), Arrange/Act/Assert, and Moq over NotImplementedException doubles. Then run:
$verifyCmd
"@
    } | ConvertTo-Json -Compress)
exit 0
