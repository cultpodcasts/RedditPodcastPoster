<#
.SYNOPSIS
  Asserts mechanical unit-test guardrails from .cursor/rules/unit-tests.mdc.

.DESCRIPTION
  Scans C# test sources for hard-fail patterns. Used by Cursor stop hooks and CI.
  Exit 0 = clean; exit 1 = violations found.

.PARAMETER Path
  One or more files/directories to scan.

.PARAMETER GitChanged
  Scan test files changed vs HEAD (unstaged + staged + untracked) or vs -BaseRef.

.PARAMETER BaseRef
  When set with -GitChanged, diff against this ref (e.g. origin/main) instead of the working tree alone.

.PARAMETER Json
  Emit a JSON object { violations: [...], ok: bool } to stdout (still exit 1 on fail).
#>
[CmdletBinding()]
param(
    [string[]] $Path,
    [switch] $GitChanged,
    [string] $BaseRef,
    [switch] $Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$BrandDenylist = @(
    'Preacher Boys',
    'Pastor Kenny Baldwin',
    'Virginia I The Age',
    'Virginia | Ep',
    'Postmormon',
    'Cults to Consciousness',
    'C2C'
)

function Test-IsTestSourcePath([string] $filePath) {
    $n = $filePath -replace '\\', '/'
    if ($n -notmatch '\.cs$') { return $false }
    if ($n -match '/obj/|/bin/') { return $false }
    # Fixture / test-support libraries are not rule-test bodies
    if ($n -match 'TestSupport/') { return $false }
    return (
        $n -match 'Tests?/' -or
        $n -match '\.Tests/' -or
        $n -match 'FunctionHost\.Tests/'
    )
}

function Get-GitChangedTestFiles([string] $base) {
    $files = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    if ($base) {
        git diff --name-only --diff-filter=ACMR "$base...HEAD" 2>$null | ForEach-Object { [void]$files.Add($_) }
        git diff --name-only --diff-filter=ACMR "$base" 2>$null | ForEach-Object { [void]$files.Add($_) }
    }
    else {
        git diff --name-only --diff-filter=ACMR HEAD 2>$null | ForEach-Object { [void]$files.Add($_) }
        git diff --name-only --cached --diff-filter=ACMR 2>$null | ForEach-Object { [void]$files.Add($_) }
        git ls-files --others --exclude-standard 2>$null | ForEach-Object { [void]$files.Add($_) }
    }

    $root = (git rev-parse --show-toplevel 2>$null)
    if (-not $root) { $root = Get-Location }
    $result = @()
    foreach ($rel in $files) {
        if (-not (Test-IsTestSourcePath $rel)) { continue }
        $full = Join-Path $root $rel
        if (Test-Path -LiteralPath $full) { $result += $full }
    }
    return $result | Sort-Object -Unique
}

function Get-ScanTargets {
    if ($GitChanged) {
        return @(Get-GitChangedTestFiles $BaseRef)
    }
    if (-not $Path -or $Path.Count -eq 0) {
        throw 'Specify -Path and/or -GitChanged.'
    }
    $targets = @()
    foreach ($p in $Path) {
        if (-not (Test-Path -LiteralPath $p)) { continue }
        $item = Get-Item -LiteralPath $p
        if ($item.PSIsContainer) {
            Get-ChildItem -LiteralPath $item.FullName -Recurse -Filter *.cs -File |
                Where-Object { Test-IsTestSourcePath $_.FullName } |
                ForEach-Object { $targets += $_.FullName }
        }
        elseif (Test-IsTestSourcePath $item.FullName) {
            $targets += $item.FullName
        }
    }
    return $targets | Sort-Object -Unique
}

function Add-Violation(
    [System.Collections.Generic.List[object]] $list,
    [string] $file,
    [int] $line,
    [string] $rule,
    [string] $detail) {
    $list.Add([pscustomobject]@{
            file   = $file
            line   = $line
            rule   = $rule
            detail = $detail
        }) | Out-Null
}

function Test-FileGuardrails([string] $filePath) {
    $violations = [System.Collections.Generic.List[object]]::new()
    $lines = Get-Content -LiteralPath $filePath
    $rel = $filePath
    try {
        $root = (git rev-parse --show-toplevel 2>$null)
        if ($root) {
            $rel = $filePath.Substring($root.Length).TrimStart('\', '/')
        }
    }
    catch { }

    $normalized = $filePath -replace '\\', '/'
    $inBusinessRules = $normalized -match '/BusinessRules/'

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $lineNum = $i + 1
        $line = $lines[$i]
        $trimmed = $line.Trim()

        if ($trimmed.StartsWith('//') -or $trimmed.StartsWith('*') -or $trimmed.StartsWith('///')) {
            continue
        }

        foreach ($brand in $BrandDenylist) {
            if ($line -like "*$brand*") {
                Add-Violation $violations $rel $lineNum 'no-production-brand-names' `
                    "Found production brand/literal '$brand'. Use scenario-shaped fixtures / generated titles."
            }
        }

        if ($line -match 'Guid\.Parse\s*\(' -or $line -match 'new\s+Guid\s*\(\s*"') {
            Add-Violation $violations $rel $lineNum 'no-hardcoded-guid' `
                'Hardcoded Guid literal. Use _fixture.CreateGuid() or specimen identity.'
        }

        if ($line -match 'new\s+DateTime\s*\(\s*20\d{2}\s*,') {
            Add-Violation $violations $rel $lineNum 'no-fixed-calendar-datetime' `
                'Fixed calendar DateTime. Use DomainTestFixture.UtcDaysAgo / UtcAtTime / UtcDateDaysAgo.'
        }

        if ($inBusinessRules -and $line -match 'throw\s+new\s+NotImplementedException\s*\(') {
            Add-Violation $violations $rel $lineNum 'no-notimplemented-in-businessrules' `
                'NotImplementedException test double in BusinessRules. Prefer Moq or a focused fake.'
        }

        # [Fact] or [Theory] without DisplayName on the same attribute
        if ($trimmed -match '^\[Fact\]\s*$' -or $trimmed -match '^\[Theory\]\s*$') {
            Add-Violation $violations $rel $lineNum 'fact-requires-displayname' `
                '[Fact]/[Theory] missing DisplayName. Use [Fact(DisplayName = "...")].'
        }
        elseif ($trimmed -match '^\[(Fact|Theory)\(' -and $trimmed -notmatch 'DisplayName\s*=') {
            # Multi-line attribute: Fact( then DisplayName on following lines
            $window = ($lines[$i..([Math]::Min($i + 6, $lines.Count - 1))] -join ' ')
            if ($window -notmatch 'DisplayName\s*=') {
                Add-Violation $violations $rel $lineNum 'fact-requires-displayname' `
                    'Fact/Theory attribute missing DisplayName.'
            }
        }
    }

    # Section comments: any public async?/Task|void test method should have Arrange nearby
    $text = $lines -join "`n"
    $methodMatches = [regex]::Matches(
        $text,
        '(?m)^\s*public\s+(?:async\s+)?(?:Task|ValueTask|void)\s+(\w+)\s*\([^)]*\)\s*\{')
    foreach ($m in $methodMatches) {
        $name = $m.Groups[1].Value
        if ($name -match '^(ToString|Equals|GetHashCode|Dispose)$') { continue }
        # Find line number of method
        $prefix = $text.Substring(0, $m.Index)
        $methodLine = ($prefix -split "`n").Count
        $bodyStart = $m.Index + $m.Length
        $slice = $text.Substring($bodyStart, [Math]::Min(800, $text.Length - $bodyStart))
        if ($slice -notmatch '//\s*Arrange') {
            Add-Violation $violations $rel $methodLine 'missing-arrange-act-assert' `
                "Test method '$name' missing '// Arrange' (require Arrange/Act/Assert section comments)."
        }
    }

    return $violations
}

$targets = @(Get-ScanTargets)
$all = [System.Collections.Generic.List[object]]::new()
foreach ($t in $targets) {
    foreach ($v in (Test-FileGuardrails $t)) {
        $all.Add($v) | Out-Null
    }
}

if ($Json) {
    [pscustomobject]@{
        ok         = ($all.Count -eq 0)
        scanned    = @($targets)
        violations = @($all)
    } | ConvertTo-Json -Depth 5
}
else {
    if ($targets.Count -eq 0) {
        Write-Host 'unit-test guardrails: no matching test files to scan.'
    }
    elseif ($all.Count -eq 0) {
        Write-Host "unit-test guardrails: OK ($($targets.Count) file(s))."
    }
    else {
        Write-Host "unit-test guardrails: $($all.Count) violation(s) in $($targets.Count) file(s):" -ForegroundColor Red
        foreach ($v in $all) {
            Write-Host ("  {0}:{1} [{2}] {3}" -f $v.file, $v.line, $v.rule, $v.detail) -ForegroundColor Red
        }
        Write-Host 'See .cursor/rules/unit-tests.mdc'
    }
}

if ($all.Count -gt 0) { exit 1 }
exit 0
