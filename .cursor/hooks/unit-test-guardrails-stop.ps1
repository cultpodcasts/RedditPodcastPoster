#Requires -Version 7
<#
  stop: if git-changed test files (or afterFileEdit failure log) violate
  unit-test guardrails, emit followup_message so the agent must fix them.
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

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

$script = Join-Path $repoRoot 'scripts\assert-unit-test-guardrails.ps1'
$jsonOut = & pwsh -NoProfile -File $script -GitChanged -Json 2>&1 | Out-String
$result = $null
try { $result = $jsonOut | ConvertFrom-Json } catch { }

$violations = @()
if ($result -and $result.violations) {
    $violations = @($result.violations)
}

$stateFile = Join-Path $PSScriptRoot '.state\unit-test-guardrail-failures.jsonl'
if (Test-Path -LiteralPath $stateFile) {
    # Include recent after-edit findings still present on disk
    Get-Content -LiteralPath $stateFile -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            $row = $_ | ConvertFrom-Json
            if ($row.violations) { $violations += @($row.violations) }
        }
        catch { }
    }
}

# De-dupe by file:line:rule
$unique = [System.Collections.Generic.List[object]]::new()
$seen = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($v in $violations) {
    $key = '{0}:{1}:{2}' -f $v.file, $v.line, $v.rule
    if ($seen.Add($key)) { $unique.Add($v) | Out-Null }
}

if ($unique.Count -eq 0) {
    if (Test-Path -LiteralPath $stateFile) {
        Remove-Item -LiteralPath $stateFile -Force -ErrorAction SilentlyContinue
    }
    Write-Output '{}'
    exit 0
}

if ($loopCount -ge 3) {
    $summary = ($unique | Select-Object -First 8 | ForEach-Object {
            '{0}:{1} [{2}] {3}' -f $_.file, $_.line, $_.rule, $_.detail
        }) -join "`n"
    Write-Output (@{
            followup_message = @"
STOP: unit-test guardrail violations remain after $loopCount fix attempts. Do not claim the test work is done.

$summary

Read .cursor/rules/unit-tests.mdc and fix every violation, then re-run:
pwsh ./scripts/assert-unit-test-guardrails.ps1 -GitChanged
"@
        } | ConvertTo-Json -Compress)
    exit 0
}

$summary = ($unique | Select-Object -First 12 | ForEach-Object {
        '{0}:{1} [{2}] {3}' -f $_.file, $_.line, $_.rule, $_.detail
    }) -join "`n"

Write-Output (@{
        followup_message = @"
Unit-test guardrail violations detected (unit-tests.mdc). Fix them before finishing.

$summary

Re-read .cursor/rules/unit-tests.mdc. Prefer DomainTestFixture specimens, Fact(DisplayName=...), Arrange/Act/Assert, and Moq over NotImplementedException doubles. Then run:
pwsh ./scripts/assert-unit-test-guardrails.ps1 -GitChanged
"@
    } | ConvertTo-Json -Compress)
exit 0
