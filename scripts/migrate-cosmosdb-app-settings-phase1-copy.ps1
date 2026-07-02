# Phase 1: Copy cosmosdbv2__* app settings to cosmosdb__* on production Function apps.
# Both prefixes coexist after this step; runtime binds the cosmosdb section (cosmosdb__*).
# Use when Infrastructure/functions.bicep is not deploying.
#
# Typical flow:
#   1. Run this script (copy v2 -> cosmosdb__)
#   2. Deploy code that reads cosmosdb__*
#   3. Run migrate-cosmosdb-app-settings-phase2-remove-v2.ps1 (delete cosmosdbv2__*)
#
# Usage:
#   az login
#   .\scripts\migrate-cosmosdb-app-settings-phase1-copy.ps1
#   .\scripts\migrate-cosmosdb-app-settings-phase1-copy.ps1 -WhatIf
#   .\scripts\migrate-cosmosdb-app-settings-phase1-copy.ps1 -SubscriptionId 'a6b8f1a2-6163-41bc-aa6d-e33928939a6e' -ResourceGroup AutomatedInfra -FunctionApps api-infra,discover-infra,indexer-infra
#
# Parameters:
#   -SubscriptionId  Optional. Sets az account before changes.
#   -ResourceGroup   Default: AutomatedInfra
#   -FunctionApps    Default: api-infra, discover-infra, indexer-infra
#   -WhatIf          Dry run (via SupportsShouldProcess)

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SubscriptionId,

    [string]$ResourceGroup = 'AutomatedInfra',

    [string[]]$FunctionApps = @('api-infra', 'discover-infra', 'indexer-infra')
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Migrate-CosmosDbAppSettingsCommon.ps1')

$account = Initialize-CosmosDbAppSettingsMigration -SubscriptionId $SubscriptionId

Write-Host "Azure subscription: $account"
Write-Host "Resource group: $ResourceGroup"
Write-Host "Function apps: $($FunctionApps -join ', ')"
Write-Host 'cosmosdbv2__* values overwrite any existing cosmosdb__* keys (stale legacy cultpodcasts-ukdb keys are replaced).'
Write-Host ''

foreach ($app in $FunctionApps) {
    Write-Host "=== $app ==="

    $allSettings = Get-FunctionAppSettings -ResourceGroup $ResourceGroup -FunctionApp $app
    $v2Settings = Get-CosmosDbV2AppSettings -AppSettings $allSettings

    if (-not $v2Settings -or $v2Settings.Count -eq 0) {
        Write-Host 'No cosmosdbv2__* settings found; nothing to copy.'
        continue
    }

    $settingsByName = @{}
    foreach ($setting in $allSettings) {
        $settingsByName[$setting.name] = $setting.value
    }

    $toApply = @()

    foreach ($v2Setting in $v2Settings) {
        $targetName = $v2Setting.name -replace '^cosmosdbv2__', 'cosmosdb__'
        $targetExists = $settingsByName.ContainsKey($targetName)

        $action = if ($targetExists) { 'Overwrite' } else { 'Copy' }
        Write-Host "$action $($v2Setting.name) -> $targetName"
        $toApply += "$targetName=$($v2Setting.value)"
    }

    if ($toApply.Count -eq 0) {
        Write-Host 'No cosmosdb__* settings to apply.'
        continue
    }

    if ($PSCmdlet.ShouldProcess($app, "Copy $($toApply.Count) cosmosdb__* app setting(s)")) {
        az functionapp config appsettings set `
            --resource-group $ResourceGroup `
            --name $app `
            --settings $toApply `
            -o none

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to update app settings for '$app' (exit code $LASTEXITCODE)."
        }
    }

    az functionapp config appsettings list `
        --resource-group $ResourceGroup `
        --name $app `
        --query "[?starts_with(name, 'cosmosdb__')].{name:name,value:value}" `
        -o table
}

Write-Host ''
Write-Host 'Phase 1 complete. cosmosdb__* and cosmosdbv2__* now coexist on updated apps.'
Write-Host 'After verifying runtime against cosmosdb__*, run migrate-cosmosdb-app-settings-phase2-remove-v2.ps1.'
