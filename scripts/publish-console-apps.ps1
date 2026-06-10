[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Runtime = 'win-x64',

    [string]$Configuration = 'Release',

    [string]$OutputDir,

    [string[]]$Exclude = @('ThrowawayConsole', 'TextClassifierTraining.Tests'),

    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot 'artifacts\tools'
}

$stagingRoot = Join-Path $repoRoot 'artifacts\.console-publish-staging'
$consoleAppsRoot = Join-Path $repoRoot 'Console-Apps'

$projects = Get-ChildItem -Path $consoleAppsRoot -Recurse -Filter '*.csproj' |
    Where-Object {
        $name = [IO.Path]::GetFileNameWithoutExtension($_.Name)
        $name -notin $Exclude -and $name -notlike '*.Tests'
    } |
    Sort-Object FullName

if ($projects.Count -eq 0) {
    throw "No console app projects found under $consoleAppsRoot"
}

if ($PSCmdlet.ShouldProcess($OutputDir, 'Prepare output directories')) {
    if (Test-Path $stagingRoot) {
        Remove-Item $stagingRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$published = [System.Collections.Generic.List[object]]::new()
$failures = [System.Collections.Generic.List[object]]::new()

foreach ($project in $projects) {
    $appName = [IO.Path]::GetFileNameWithoutExtension($project.Name)
    $profile = 'SelfContained'
    $projectXml = [xml](Get-Content -LiteralPath $project.FullName -Raw)
    foreach ($group in @($projectXml.Project.PropertyGroup)) {
        if ($null -ne $group.ConsoleAppPublishProfile -and [string]$group.ConsoleAppPublishProfile) {
            $profile = [string]$group.ConsoleAppPublishProfile
            break
        }
    }

    $stageDir = Join-Path $stagingRoot $appName
    $publishArgs = @(
        'publish',
        $project.FullName,
        '--configuration', $Configuration,
        '--runtime', $Runtime,
        '--output', $stageDir,
        '-m:1',
        '-p:DebugType=none',
        '-p:SkipEnsureDependencyInjectionBuilt=true'
    )
    if ($NoRestore) {
        $publishArgs += '--no-restore'
    }

    Write-Host "Publishing $appName ($profile)..." -ForegroundColor Cyan
    try {
        $attempt = 0
        do {
            $attempt++
            & dotnet @publishArgs
            $publishExitCode = $LASTEXITCODE
            if ($publishExitCode -eq 0) {
                break
            }
            if ($attempt -lt 3) {
                Write-Host "Retrying $appName (attempt $($attempt + 1))..." -ForegroundColor Yellow
                Start-Sleep -Seconds 3
            }
        } while ($attempt -lt 3)

        if ($publishExitCode -ne 0) {
            throw "dotnet publish exited with code $publishExitCode"
        }

        $exeName = "$appName.exe"
        $builtExe = Join-Path $stageDir $exeName
        if (-not (Test-Path $builtExe)) {
            throw "Expected executable was not produced: $builtExe"
        }

        $destExe = Join-Path $OutputDir $exeName
        if ($PSCmdlet.ShouldProcess($destExe, "Copy $exeName")) {
            Copy-Item -LiteralPath $builtExe -Destination $destExe -Force
        }

        $published.Add([pscustomobject]@{
            Name = $appName
            Profile = $profile
            Executable = $destExe
        })
    }
    catch {
        Write-Warning "Failed to publish ${appName}: $($_.Exception.Message)"
        $failures.Add([pscustomobject]@{
            Name = $appName
            Profile = $profile
            Error = $_.Exception.Message
        })
    }
}

if ($PSCmdlet.ShouldProcess($stagingRoot, 'Remove staging directory')) {
    if (Test-Path $stagingRoot) {
        Remove-Item $stagingRoot -Recurse -Force
    }
}

Write-Host ''
Write-Host "Published $($published.Count) tool(s) to $OutputDir" -ForegroundColor Green
if ($published.Count -gt 0) {
    $published | Format-Table Name, Profile -AutoSize
}

if ($failures.Count -gt 0) {
    Write-Warning "$($failures.Count) publish failure(s):"
    $failures | Format-Table Name, Profile, Error -AutoSize
    exit 1
}
