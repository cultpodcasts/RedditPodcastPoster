[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Position = 0)]
    [string[]]$App,

    [string]$Runtime = 'win-x64',

    [string]$Configuration = 'Release',

    [string]$OutputDir,

    [string[]]$Exclude = @('ThrowawayConsole'),

    # Skip the upfront restore (assumes packages are already restored).
    [switch]$NoRestore,

    # Publish apps concurrently after a sequential pre-build. Default stays sequential
    # to avoid file-lock races when multiple publishes compile shared ProjectReferences.
    [switch]$Parallel,

    # Max concurrent dotnet publish processes when -Parallel is set.
    [int]$ThrottleLimit = 4
)

$ErrorActionPreference = 'Stop'

if ($ThrottleLimit -lt 1) {
    throw "-ThrottleLimit must be >= 1"
}

if ($Parallel -and $PSVersionTable.PSVersion.Major -lt 7) {
    throw "-Parallel requires PowerShell 7+ (ForEach-Object -Parallel). Current: $($PSVersionTable.PSVersion). Re-run under pwsh, or omit -Parallel."
}

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

function Get-ConsoleAppPublishProfile {
    param([string]$ProjectPath)

    # Directory.Build.props applies the profile via MSBuild; this is for logging only.
    $profile = 'SelfContained'
    $projectXml = [xml](Get-Content -LiteralPath $ProjectPath -Raw)
    foreach ($group in @($projectXml.Project.PropertyGroup)) {
        if ($null -ne $group.ConsoleAppPublishProfile -and [string]$group.ConsoleAppPublishProfile) {
            return [string]$group.ConsoleAppPublishProfile
        }
    }
    return $profile
}

function Invoke-DotNetWithRetry {
    param(
        [string]$AppName,
        [string[]]$DotNetArgs,
        [int]$MaxAttempts = 3
    )

    $attempt = 0
    $exitCode = 1
    do {
        $attempt++
        & dotnet @DotNetArgs
        $exitCode = $LASTEXITCODE
        if ($exitCode -eq 0) {
            return
        }
        if ($attempt -lt $MaxAttempts) {
            Write-Host "Retrying $AppName (attempt $($attempt + 1))..." -ForegroundColor Yellow
            Start-Sleep -Seconds 3
        }
    } while ($attempt -lt $MaxAttempts)

    throw "dotnet $($DotNetArgs[0]) exited with code $exitCode"
}

function Publish-ConsoleApp {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [System.IO.FileInfo]$Project,
        [string]$StagingRoot,
        [string]$OutputDir,
        [string]$Configuration,
        [string]$Runtime,
        [bool]$NoBuild,
        [bool]$BuildProjectReferences,
        # Keep single-node MSBuild when compiling ProjectReferences to reduce bin/obj lock risk.
        [bool]$SingleNodeMsBuild
    )

    $appName = [IO.Path]::GetFileNameWithoutExtension($Project.Name)
    $profile = Get-ConsoleAppPublishProfile -ProjectPath $Project.FullName
    $stageDir = Join-Path $StagingRoot $appName

    # Native AOT compilation runs during publish, not build — never use --no-build for it.
    if ($profile -eq 'NativeAot') {
        $NoBuild = $false
    }

    $publishArgs = @(
        'publish',
        $Project.FullName,
        '--configuration', $Configuration,
        '--runtime', $Runtime,
        '--output', $stageDir,
        '--no-restore',
        '-p:DebugType=none',
        '-p:SkipEnsureDependencyInjectionBuilt=true'
    )
    if ($SingleNodeMsBuild) {
        $publishArgs += '-m:1'
    }
    if (-not $BuildProjectReferences) {
        $publishArgs += '-p:BuildProjectReferences=false'
    }
    if ($NoBuild) {
        $publishArgs += '--no-build'
    }

    Write-Host "Publishing $appName ($profile)..." -ForegroundColor Cyan
    Invoke-DotNetWithRetry -AppName $appName -DotNetArgs $publishArgs

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

    return [pscustomobject]@{
        Name = $appName
        Profile = $profile
        Executable = $destExe
    }
}

if ($PSCmdlet.ShouldProcess($OutputDir, 'Prepare output directories')) {
    if (Test-Path $stagingRoot) {
        Remove-Item $stagingRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# One restore for the whole set beats 25× restore-during-publish (NuGet is otherwise re-evaluated each time).
if (-not $NoRestore) {
    if ($PSCmdlet.ShouldProcess("$($projects.Count) console app(s)", 'dotnet restore')) {
        Write-Host "Restoring $($projects.Count) console app(s)..." -ForegroundColor Cyan
        foreach ($project in $projects) {
            $appName = [IO.Path]::GetFileNameWithoutExtension($project.Name)
            & dotnet restore $project.FullName --runtime $Runtime
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet restore failed for $appName (exit $LASTEXITCODE)"
            }
        }
    }
}

$published = [System.Collections.Generic.List[object]]::new()
$failures = [System.Collections.Generic.List[object]]::new()

$publishTarget = if ($Parallel) {
    "publish $($projects.Count) console app(s) (parallel, throttle $ThrottleLimit)"
} else {
    "publish $($projects.Count) console app(s) (sequential)"
}

if ($PSCmdlet.ShouldProcess($OutputDir, $publishTarget)) {
if ($Parallel) {
    # Sequential compile first so shared class libraries are built once; parallel publish then
    # only packages each app (--no-build / BuildProjectReferences=false) and avoids compile races.
    Write-Host "Pre-building $($projects.Count) app(s) for parallel publish (ThrottleLimit=$ThrottleLimit)..." -ForegroundColor Cyan
    foreach ($project in $projects) {
        $appName = [IO.Path]::GetFileNameWithoutExtension($project.Name)
        Write-Host "Building $appName..." -ForegroundColor DarkCyan
        $buildArgs = @(
            'build',
            $project.FullName,
            '--configuration', $Configuration,
            '--runtime', $Runtime,
            '--no-restore',
            '-p:DebugType=none',
            '-p:SkipEnsureDependencyInjectionBuilt=true'
        )
        try {
            Invoke-DotNetWithRetry -AppName $appName -DotNetArgs $buildArgs
        }
        catch {
            Write-Warning "Failed to build ${appName}: $($_.Exception.Message)"
            $failures.Add([pscustomobject]@{
                Name = $appName
                Profile = (Get-ConsoleAppPublishProfile -ProjectPath $project.FullName)
                Error = $_.Exception.Message
            })
        }
    }

    $toPublish = @($projects | Where-Object {
        $name = [IO.Path]::GetFileNameWithoutExtension($_.Name)
        -not ($failures | Where-Object { $_.Name -eq $name })
    })

    $syncPublished = [System.Collections.Concurrent.ConcurrentBag[object]]::new()
    $syncFailures = [System.Collections.Concurrent.ConcurrentBag[object]]::new()

    $toPublish | ForEach-Object -Parallel {
        $project = $_
        $appName = [IO.Path]::GetFileNameWithoutExtension($project.Name)
        $profile = 'SelfContained'
        try {
            # Re-implement publish inline: ForEach-Object -Parallel cannot call parent functions.
            $projectXml = [xml](Get-Content -LiteralPath $project.FullName -Raw)
            foreach ($group in @($projectXml.Project.PropertyGroup)) {
                if ($null -ne $group.ConsoleAppPublishProfile -and [string]$group.ConsoleAppPublishProfile) {
                    $profile = [string]$group.ConsoleAppPublishProfile
                    break
                }
            }

            $stageDir = Join-Path $using:stagingRoot $appName
            $publishArgs = @(
                'publish',
                $project.FullName,
                '--configuration', $using:Configuration,
                '--runtime', $using:Runtime,
                '--output', $stageDir,
                '--no-restore',
                '-p:BuildProjectReferences=false',
                '-p:DebugType=none',
                '-p:SkipEnsureDependencyInjectionBuilt=true'
            )
            # AOT native compile happens at publish time; SelfContained can reuse the pre-build.
            if ($profile -ne 'NativeAot') {
                $publishArgs += '--no-build'
            }

            Write-Host "Publishing $appName ($profile)..." -ForegroundColor Cyan
            $attempt = 0
            $publishExitCode = 1
            do {
                $attempt++
                & dotnet @publishArgs
                $publishExitCode = $LASTEXITCODE
                if ($publishExitCode -eq 0) { break }
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

            $destExe = Join-Path $using:OutputDir $exeName
            Copy-Item -LiteralPath $builtExe -Destination $destExe -Force

            $appsettings = Join-Path $stageDir 'appsettings.json'
            if (Test-Path $appsettings) {
                Copy-Item -LiteralPath $appsettings -Destination (Join-Path $using:OutputDir "$appName.appsettings.json") -Force
            }

            ($using:syncPublished).Add([pscustomobject]@{
                Name = $appName
                Profile = $profile
                Executable = $destExe
            })
        }
        catch {
            Write-Warning "Failed to publish ${appName}: $($_.Exception.Message)"
            ($using:syncFailures).Add([pscustomobject]@{
                Name = $appName
                Profile = $profile
                Error = $_.Exception.Message
            })
        }
    } -ThrottleLimit $ThrottleLimit

    foreach ($item in $syncPublished) { $published.Add($item) }
    foreach ($item in $syncFailures) { $failures.Add($item) }
}
else {
    # Sequential (default): restore already done; allow MSBuild to use multiple nodes within each
    # publish so ProjectReferences compile concurrently. Use -Parallel for cross-app concurrency.
    foreach ($project in $projects) {
        $appName = [IO.Path]::GetFileNameWithoutExtension($project.Name)
        try {
            $result = Publish-ConsoleApp `
                -Project $project `
                -StagingRoot $stagingRoot `
                -OutputDir $OutputDir `
                -Configuration $Configuration `
                -Runtime $Runtime `
                -NoBuild:$false `
                -BuildProjectReferences:$true `
                -SingleNodeMsBuild:$false
            $published.Add($result)
        }
        catch {
            Write-Warning "Failed to publish ${appName}: $($_.Exception.Message)"
            $failures.Add([pscustomobject]@{
                Name = $appName
                Profile = (Get-ConsoleAppPublishProfile -ProjectPath $project.FullName)
                Error = $_.Exception.Message
            })
        }
    }
}
} # ShouldProcess publish

if ($PSCmdlet.ShouldProcess($stagingRoot, 'Remove staging directory')) {
    if (Test-Path $stagingRoot) {
        Remove-Item $stagingRoot -Recurse -Force
    }
}

Write-Host ''
Write-Host "Published $($published.Count) tool(s) to $OutputDir" -ForegroundColor Green
if ($published.Count -gt 0) {
    $published | Sort-Object Name | Format-Table Name, Profile -AutoSize
}

if ($failures.Count -gt 0) {
    Write-Warning "$($failures.Count) publish failure(s):"
    $failures | Sort-Object Name | Format-Table Name, Profile, Error -AutoSize
    exit 1
}
