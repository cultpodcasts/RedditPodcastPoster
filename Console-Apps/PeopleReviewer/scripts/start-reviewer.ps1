<#
.SYNOPSIS
  Starts PeopleReviewer and records its PID under .local/
#>
param(
    [string]$SeedPath = "",
    [int]$Port = 5188
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$localDir = Join-Path $repoRoot ".local"
New-Item -ItemType Directory -Force -Path $localDir | Out-Null

$pidFile = Join-Path $localDir "people-reviewer.pid"
$stopScript = Join-Path $localDir "stop-people-reviewer.ps1"
$outLog = Join-Path $localDir "people-reviewer.out.log"
$errLog = Join-Path $localDir "people-reviewer.err.log"

if (Test-Path $pidFile) {
    $existing = Get-Content $pidFile -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($existing -and (Get-Process -Id $existing -ErrorAction SilentlyContinue)) {
        Write-Host "PeopleReviewer already running (PID $existing). Stop with .\.local\stop-people-reviewer.ps1"
        exit 1
    }
}

$project = Join-Path $repoRoot "Console-Apps\PeopleReviewer\PeopleReviewer.csproj"
$argList = @("run", "--project", $project, "--no-build", "--", "--port", "$Port")
if ($SeedPath) {
    $argList += @("--seed-path", $SeedPath)
}

# Build first so --no-build is reliable
dotnet build $project -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$proc = Start-Process -FilePath "dotnet" -ArgumentList $argList `
    -WorkingDirectory $repoRoot `
    -RedirectStandardOutput $outLog `
    -RedirectStandardError $errLog `
    -PassThru -WindowStyle Hidden

$proc.Id | Set-Content -Path $pidFile -Encoding ascii

@"
# Auto-generated — stops PeopleReviewer started by start-reviewer.ps1
`$pidFile = Join-Path `$PSScriptRoot "people-reviewer.pid"
if (-not (Test-Path `$pidFile)) {
    Write-Host "No PID file at `$pidFile"
    exit 0
}
`$procId = Get-Content `$pidFile | Select-Object -First 1
if (`$procId -and (Get-Process -Id `$procId -ErrorAction SilentlyContinue)) {
    Stop-Process -Id `$procId -Force
    Write-Host "Stopped PeopleReviewer PID `$procId"
} else {
    Write-Host "Process `$procId not running"
}
Remove-Item `$pidFile -Force -ErrorAction SilentlyContinue
"@ | Set-Content -Path $stopScript -Encoding utf8

Write-Host "PeopleReviewer started PID $($proc.Id)"
Write-Host "URL: http://127.0.0.1:$Port"
Write-Host "Log: $outLog / $errLog"
Write-Host "Stop: .\.local\stop-people-reviewer.ps1"
if ($SeedPath) {
    Write-Host "Seed: $SeedPath"
}
