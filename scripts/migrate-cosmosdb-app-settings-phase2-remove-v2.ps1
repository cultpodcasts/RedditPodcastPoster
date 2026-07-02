# Phase 2: Remove all cosmosdbv2__* app settings from production Function apps.
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
Write-Host ''

foreach ($app in $FunctionApps) {
    Write-Host "=== $app ==="

    $allSettings = Get-FunctionAppSettings -ResourceGroup $ResourceGroup -FunctionApp $app
    $v2Settings = Get-CosmosDbV2AppSettings -AppSettings $allSettings

    if (-not $v2Settings -or $v2Settings.Count -eq 0) {
        Write-Host 'No cosmosdbv2__* settings found; nothing to remove.'
        continue
    }

    $v2Names = @($v2Settings | ForEach-Object { $_.name })
    Write-Host "Removing $($v2Names.Count) cosmosdbv2__* setting(s):"
    $v2Names | ForEach-Object { Write-Host "  $_" }

    if ($PSCmdlet.ShouldProcess($app, "Remove $($v2Names.Count) cosmosdbv2__* app setting(s)")) {
        az functionapp config appsettings delete `
            --resource-group $ResourceGroup `
            --name $app `
            --setting-names $v2Names `
            -o none

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to delete app settings for '$app' (exit code $LASTEXITCODE)."
        }
    }

    $remaining = az functionapp config appsettings list `
        --resource-group $ResourceGroup `
        --name $app `
        --query "[?starts_with(name, 'cosmosdbv2__')].name" `
        -o tsv

    if ($remaining) {
        Write-Warning "cosmosdbv2__* keys still present on '$app': $($remaining -join ', ')"
    } else {
        Write-Host 'No cosmosdbv2__* settings remain.'
    }
}

Write-Host ''
Write-Host 'Phase 2 complete. cosmosdbv2__* settings removed from updated apps.'
