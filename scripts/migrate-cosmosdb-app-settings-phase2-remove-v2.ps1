# Phase 2: Remove cosmosdbv2__* and stale legacy cosmosdb__* app settings from production Function apps.
# Run only after code is deployed and verified against cosmosdb__* settings.
# Use when Infrastructure/functions.bicep is not deploying.
#
# Usage:
#   az login
#   .\scripts\migrate-cosmosdb-app-settings-phase2-remove-v2.ps1
#   .\scripts\migrate-cosmosdb-app-settings-phase2-remove-v2.ps1 -WhatIf
#   .\scripts\migrate-cosmosdb-app-settings-phase2-remove-v2.ps1 -SubscriptionId 'a6b8f1a2-6163-41bc-aa6d-e33928939a6e' -ResourceGroup AutomatedInfra -FunctionApps api-infra,discover-infra,indexer-infra
#
# Parameters:
#   -SubscriptionId  Optional. Sets az account before changes.
#   -ResourceGroup   Default: AutomatedInfra
#   -FunctionApps    Default: api-infra, discover-infra, indexer-infra
#   -BackupPath      Optional. Folder for pre-flight JSON backup (default: scripts/.app-settings-backups/<timestamp>/)
#   -SkipBackup      Skip automatic backup (not recommended for production)
#   -WhatIf          Dry run (via SupportsShouldProcess)

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SubscriptionId,

    [string]$ResourceGroup = 'AutomatedInfra',

    [string[]]$FunctionApps = @('api-infra', 'discover-infra', 'indexer-infra'),

    [string]$BackupPath,

    [switch]$SkipBackup
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Migrate-CosmosDbAppSettingsCommon.ps1')

# Pre-split-database keys superseded by cosmosdbv2__* / canonical cosmosdb__* container settings.
$staleLegacyCosmosDbSettingNames = @(
    'cosmosdb__Container'
    'cosmosdb__UseGateWay'
)

$account = Initialize-CosmosDbAppSettingsMigration -SubscriptionId $SubscriptionId

Write-Host "Azure subscription: $account"
Write-Host "Resource group: $ResourceGroup"
Write-Host "Function apps: $($FunctionApps -join ', ')"
Write-Host ''

$resolvedBackupPath = $null
if (-not $SkipBackup) {
    $resolvedBackupPath = Resolve-CosmosDbAppSettingsBackupPath -BackupPath $BackupPath
    if ($PSCmdlet.ShouldProcess($resolvedBackupPath, 'Export pre-flight app settings backup')) {
        Export-FunctionAppSettingsBackup `
            -ResourceGroup $ResourceGroup `
            -FunctionApps $FunctionApps `
            -BackupPath $resolvedBackupPath `
            -PhaseLabel 'phase2-remove-v2' | Out-Null
    }
    Write-Host ''
}

foreach ($app in $FunctionApps) {
    Write-Host "=== $app ==="

    $allSettings = Get-FunctionAppSettings -ResourceGroup $ResourceGroup -FunctionApp $app
    $v2Settings = Get-CosmosDbV2AppSettings -AppSettings $allSettings

    $settingsByName = @{}
    foreach ($setting in $allSettings) {
        $settingsByName[$setting.name] = $setting.value
    }

    $v2Names = @($v2Settings | ForEach-Object { $_.name })
    $staleLegacyPresent = @(
        $staleLegacyCosmosDbSettingNames | Where-Object { $settingsByName.ContainsKey($_) }
    )

    $toRemove = @($v2Names + $staleLegacyPresent | Select-Object -Unique)

    if ($toRemove.Count -eq 0) {
        Write-Host 'No cosmosdbv2__* or stale legacy cosmosdb__* settings found; nothing to remove.'
        continue
    }

    Write-Host "Removing $($toRemove.Count) setting(s):"
    $toRemove | ForEach-Object { Write-Host "  $_" }

    if ($PSCmdlet.ShouldProcess($app, "Remove $($toRemove.Count) legacy cosmos app setting(s)")) {
        az functionapp config appsettings delete `
            --resource-group $ResourceGroup `
            --name $app `
            --setting-names $toRemove `
            -o none

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to delete app settings for '$app' (exit code $LASTEXITCODE)."
        }
    }

    $remaining = az functionapp config appsettings list `
        --resource-group $ResourceGroup `
        --name $app `
        --query "[?starts_with(name, 'cosmosdbv2__') || name=='cosmosdb__Container' || name=='cosmosdb__UseGateWay'].name" `
        -o tsv

    if ($remaining) {
        Write-Warning "Legacy cosmos keys still present on '$app': $($remaining -join ', ')"
    } else {
        Write-Host 'No cosmosdbv2__* or stale legacy cosmosdb__* settings remain.'
    }
}

Write-Host ''
Write-Host 'Phase 2 complete. Legacy cosmosdbv2__* and stale cosmosdb__* settings removed from updated apps.'
if ($resolvedBackupPath) {
    Write-Host "Rollback: .\scripts\restore-cosmosdb-app-settings-from-backup.ps1 -BackupPath '$resolvedBackupPath'"
}
