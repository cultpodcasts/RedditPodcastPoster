#Requires -Version 7
<#
  afterFileEdit: if the edited file is a test source, run guardrail scan and
  append violations to .cursor/hooks/.unit-test-guardrail-failures.jsonl so the
  stop hook can force a fix loop.
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

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

$script = Join-Path $repoRoot 'scripts\assert-unit-test-guardrails.ps1'
$out = & pwsh -NoProfile -File $script -Path $filePath -Json 2>&1 | Out-String
$result = $null
try { $result = $out | ConvertFrom-Json } catch { }

if ($result -and -not $result.ok -and $result.violations) {
    $stateDir = Join-Path $PSScriptRoot '.state'
    New-Item -ItemType Directory -Force -Path $stateDir | Out-Null
    $stateFile = Join-Path $stateDir 'unit-test-guardrail-failures.jsonl'
    $entry = [pscustomobject]@{
        at         = [DateTime]::UtcNow.ToString('o')
        file       = $filePath
        violations = $result.violations
    }
    Add-Content -LiteralPath $stateFile -Value ($entry | ConvertTo-Json -Compress -Depth 6)
}

Write-Output '{}'
exit 0
