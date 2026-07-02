# Restore Function app settings from a JSON backup created by the cosmosdb migration scripts.
# Use after a failed or incorrect phase1/phase2 run. Restores via az appsettings set (merge).
#
# Usage:
#   az login
#   .\scripts\restore-cosmosdb-app-settings-from-backup.ps1 -BackupPath .\scripts\.app-settings-backups\20260702-143000
#   .\scripts\restore-cosmosdb-app-settings-from-backup.ps1 -BackupPath .\scripts\.app-settings-backups\20260702-143000 -WhatIf
#   .\scripts\restore-cosmosdb-app-settings-from-backup.ps1 -BackupPath .\backup\api-infra-appsettings.json -FunctionApps api-infra
#
# Parameters:
#   -BackupPath     Folder from Export-FunctionAppSettingsBackup, or a single *-appsettings.json file
#   -ResourceGroup  Default: AutomatedInfra
#   -FunctionApps   Default: all apps found in backup folder (or inferred from single file)
#   -CosmosKeysOnly Restore only cosmosdb__* and cosmosdbv2__* keys (leave other settings untouched)
#   -WhatIf         Dry run (via SupportsShouldProcess)

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,

    [string]$ResourceGroup = 'AutomatedInfra',

    [string[]]$FunctionApps,

    [switch]$CosmosKeysOnly
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Migrate-CosmosDbAppSettingsCommon.ps1')

$null = Initialize-CosmosDbAppSettingsMigration

$resolvedPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($BackupPath)

if (-not (Test-Path -LiteralPath $resolvedPath)) {
    throw "Backup path not found: $resolvedPath"
}

$backupFiles = @()
if ((Get-Item -LiteralPath $resolvedPath).PSIsContainer) {
    $backupFiles = @(Get-ChildItem -LiteralPath $resolvedPath -Filter '*-appsettings.json' | Sort-Object Name)
    if ($backupFiles.Count -eq 0) {
        throw "No *-appsettings.json files found under: $resolvedPath"
    }
} else {
    $backupFiles = @(Get-Item -LiteralPath $resolvedPath)
}

if (-not $FunctionApps -or $FunctionApps.Count -eq 0) {
    $FunctionApps = @($backupFiles | ForEach-Object {
        if ($_.BaseName -match '^(.*)-appsettings$') {
            $Matches[1]
        } else {
            throw "Cannot infer function app name from backup file '$($_.Name)'. Pass -FunctionApps explicitly."
        }
    })
}

Write-Host "Resource group: $ResourceGroup"
Write-Host "Function apps: $($FunctionApps -join ', ')"
if ($CosmosKeysOnly) {
    Write-Host 'Scope: cosmosdb__* and cosmosdbv2__* keys only'
}
Write-Host ''

foreach ($app in $FunctionApps) {
    $file = if ((Get-Item -LiteralPath $resolvedPath).PSIsContainer) {
        Join-Path $resolvedPath "$app-appsettings.json"
    } else {
        $resolvedPath
    }

    Write-Host "=== $app ==="
    Import-FunctionAppSettingsFromBackup `
        -ResourceGroup $ResourceGroup `
        -FunctionApp $app `
        -BackupFile $file `
        -CosmosKeysOnly:$CosmosKeysOnly
}

Write-Host ''
Write-Host 'Restore complete. Function apps restart to pick up changes.'
Write-Host 'Verify cosmos keys:'
Write-Host "  az functionapp config appsettings list -g $ResourceGroup -n <app> --query `"[?starts_with(name, 'cosmosdb')].name`" -o tsv"
