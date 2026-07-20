# Publish console apps as standalone executables under artifacts\tools (for PATH).
# App inventory, CLI modes, and flags: Console-Apps/README.md
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Position = 0)]
    [string[]]$App,

    [string]$Runtime = 'win-x64',

    [string]$Configuration = 'Release',

    [string]$OutputDir,

    [string[]]$Exclude = @('ThrowawayConsole'),

    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot 'artifacts\tools'
}

$stagingRoot = Join-Path $repoRoot 'artifacts\.console-publish-staging'
$consoleAppsRoot = Join-Path $repoRoot 'Console-Apps'

$projects = @(Get-ChildItem -Path $consoleAppsRoot -Recurse -Filter '*.csproj' |
    Where-Object {
        $name = [IO.Path]::GetFileNameWithoutExtension($_.Name)
        $name -notin $Exclude -and $name -notlike '*.Tests'
    } |
    Sort-Object FullName)

$availableApps = $projects | ForEach-Object {
    [IO.Path]::GetFileNameWithoutExtension($_.Name)
} | Sort-Object -Unique

if ($App -and @($App).Count -gt 0) {
    $appFilter = @($App | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    $appFilterLower = $appFilter | ForEach-Object { $_.ToLowerInvariant() }
    $projects = @($projects | Where-Object {
        $name = [IO.Path]::GetFileNameWithoutExtension($_.Name).ToLowerInvariant()
        $appFilterLower -contains $name
    })
    $foundNames = $projects | ForEach-Object {
        [IO.Path]::GetFileNameWithoutExtension($_.Name).ToLowerInvariant()
    } | Select-Object -Unique
    $missing = $appFilter | Where-Object {
        $_.ToLowerInvariant() -notin $foundNames
    }
    if ($missing.Count -gt 0) {
        throw "Console app(s) not found: $($missing -join ', '). Available apps: $($availableApps -join ', ')"
    }
}

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

        $appsettings = Join-Path $stageDir 'appsettings.json'
        if (Test-Path $appsettings) {
            $destSettings = Join-Path $OutputDir "$appName.appsettings.json"
            if ($PSCmdlet.ShouldProcess($destSettings, "Copy $appName.appsettings.json")) {
                Copy-Item -LiteralPath $appsettings -Destination $destSettings -Force
            }
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
