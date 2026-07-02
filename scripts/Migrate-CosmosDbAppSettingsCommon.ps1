function Initialize-CosmosDbAppSettingsMigration {
    [CmdletBinding()]
    param(
        [string]$SubscriptionId
    )

    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw 'Azure CLI (az) was not found on PATH.'
    }

    $account = az account show --query name -o tsv 2>$null
    if (-not $account) {
        throw 'Azure CLI is not logged in. Run az login first.'
    }

    if ($SubscriptionId) {
        Write-Host "Setting Azure subscription: $SubscriptionId"
        az account set --subscription $SubscriptionId | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to set subscription '$SubscriptionId' (exit code $LASTEXITCODE)."
        }

        $account = az account show --query name -o tsv
    }

    return $account
}

function Get-FunctionAppSettings {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResourceGroup,

        [Parameter(Mandatory = $true)]
        [string]$FunctionApp
    )

    $json = az functionapp config appsettings list `
        --resource-group $ResourceGroup `
        --name $FunctionApp `
        -o json

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to list app settings for '$FunctionApp' (exit code $LASTEXITCODE)."
    }

    return $json | ConvertFrom-Json
}

function Get-CosmosDbV2AppSettings {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$AppSettings
    )

    return @($AppSettings | Where-Object { $_.name -like 'cosmosdbv2__*' })
}

function Resolve-CosmosDbAppSettingsBackupPath {
    param(
        [string]$BackupPath
    )

    if ($BackupPath) {
        $resolved = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($BackupPath)
        if (-not (Test-Path -LiteralPath $resolved)) {
            New-Item -ItemType Directory -Path $resolved -Force | Out-Null
        }

        return $resolved
    }

    $defaultRoot = Join-Path $PSScriptRoot '.app-settings-backups'
    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $resolved = Join-Path $defaultRoot $timestamp
    New-Item -ItemType Directory -Path $resolved -Force | Out-Null
    return $resolved
}

function Export-FunctionAppSettingsBackup {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResourceGroup,

        [Parameter(Mandatory = $true)]
        [string[]]$FunctionApps,

        [Parameter(Mandatory = $true)]
        [string]$BackupPath,

        [string]$PhaseLabel = 'migration'
    )

    $manifest = [ordered]@{
        exportedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        resourceGroup = $ResourceGroup
        functionApps  = $FunctionApps
        phase         = $PhaseLabel
        files         = @()
    }

    foreach ($app in $FunctionApps) {
        $settings = @(Get-FunctionAppSettings -ResourceGroup $ResourceGroup -FunctionApp $app)
        if ($settings.Count -eq 0) {
            throw "Export for '$app' returned no app settings; refusing to continue without a valid backup."
        }

        $fileName = "$app-appsettings.json"
        $filePath = Join-Path $BackupPath $fileName

        $settings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $filePath -Encoding utf8

        $written = @(Get-Content -LiteralPath $filePath -Raw | ConvertFrom-Json)
        if ($written.Count -ne $settings.Count) {
            throw "Backup file '$fileName' count mismatch (exported $($settings.Count), file has $($written.Count))."
        }

        $manifest.files += $fileName

        Write-Host "Backed up $($settings.Count) setting(s) for '$app' -> $filePath"
    }

    if ($manifest.files.Count -ne $FunctionApps.Count) {
        throw "Backup manifest lists $($manifest.files.Count) file(s) but $($FunctionApps.Count) function app(s) were requested."
    }

    foreach ($app in $FunctionApps) {
        $expectedFile = "$app-appsettings.json"
        if ($manifest.files -notcontains $expectedFile) {
            throw "Expected backup file '$expectedFile' missing from manifest."
        }

        $expectedPath = Join-Path $BackupPath $expectedFile
        if (-not (Test-Path -LiteralPath $expectedPath)) {
            throw "Expected backup file not found on disk: $expectedPath"
        }
    }

    $manifestPath = Join-Path $BackupPath 'manifest.json'
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    Write-Host "Backup manifest: $manifestPath"

    return $BackupPath
}

function Import-FunctionAppSettingsFromBackup {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResourceGroup,

        [Parameter(Mandatory = $true)]
        [string]$FunctionApp,

        [Parameter(Mandatory = $true)]
        [string]$BackupFile,

        [switch]$CosmosKeysOnly
    )

    if (-not (Test-Path -LiteralPath $BackupFile)) {
        throw "Backup file not found: $BackupFile"
    }

    $settings = Get-Content -LiteralPath $BackupFile -Raw | ConvertFrom-Json

    if ($CosmosKeysOnly) {
        $settings = @($settings | Where-Object {
            $_.name -like 'cosmosdb__*' -or $_.name -like 'cosmosdbv2__*'
        })
    }

    if (-not $settings -or $settings.Count -eq 0) {
        Write-Host "No settings to restore for '$FunctionApp' from $BackupFile."
        return
    }

    $pairs = @($settings | ForEach-Object { "$($_.name)=$($_.value)" })

    if ($PSCmdlet.ShouldProcess($FunctionApp, "Restore $($pairs.Count) app setting(s) from backup")) {
        az functionapp config appsettings set `
            --resource-group $ResourceGroup `
            --name $FunctionApp `
            --settings $pairs `
            -o none

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to restore app settings for '$FunctionApp' (exit code $LASTEXITCODE)."
        }
    }
}
