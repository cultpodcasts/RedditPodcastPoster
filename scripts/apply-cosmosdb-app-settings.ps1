# Applies canonical cosmosdb__* app settings on Function apps when Infrastructure/functions.bicep is not deploying.
# Mirrors the cosmosdb var in Infrastructure/functions.bicep.
#
# One-time migration (PR #870): production apps may still have cosmosdbv2__* plus stale legacy cosmosdb__*
# (old single-container cultpodcasts-ukdb). Code now binds to cosmosdb only.
#
# Recommended interim flow after merging PR #870:
#   1. .\scripts\apply-cosmosdb-app-settings.ps1 -WhatIf   # preview
#   2. .\scripts\apply-cosmosdb-app-settings.ps1           # migrate from live cosmosdbv2__* values
#   3. .\scripts\deploy-api.ps1 (and discover/indexer as needed)
#
# From Key Vault instead of copying live cosmosdbv2__* (mirrors functions.bicepparam secret names):
#   .\scripts\apply-cosmosdb-app-settings.ps1 -FromKeyVault
#
# Old -> new mapping (suffix unchanged; section prefix renamed):
#   cosmosdbv2__Endpoint                  -> cosmosdb__Endpoint
#   cosmosdbv2__AuthKeyOrResourceToken    -> cosmosdb__AuthKeyOrResourceToken
#   cosmosdbv2__DatabaseId                -> cosmosdb__DatabaseId
#   cosmosdbv2__PodcastsContainer         -> cosmosdb__PodcastsContainer
#   cosmosdbv2__EpisodesContainer         -> cosmosdb__EpisodesContainer
#   cosmosdbv2__SubjectsContainer         -> cosmosdb__SubjectsContainer
#   cosmosdbv2__ActivitiesContainer       -> cosmosdb__ActivitiesContainer
#   cosmosdbv2__DiscoveryContainer        -> cosmosdb__DiscoveryContainer
#   cosmosdbv2__LookUpsContainer          -> cosmosdb__LookUpsContainer
#   cosmosdbv2__PushSubscriptionsContainer -> cosmosdb__PushSubscriptionsContainer
#   cosmosdbv2__UseGateway                -> cosmosdb__UseGateway
#
# Stale keys removed (legacy pre-split database; not used after migration):
#   cosmosdb__Container, cosmosdb__UseGateWay, all cosmosdbv2__*

[CmdletBinding(SupportsShouldProcess = $true, DefaultParameterSetName = 'FromExistingV2')]
param(
    [string]$ResourceGroup = 'AutomatedInfra',

    [string[]]$FunctionApps = @('api-infra', 'discover-infra', 'indexer-infra'),

    [string]$KeyVaultName = 'cultpodcasts-deployment',

    [string]$KeyVaultResourceGroup = 'Management',

    [Parameter(ParameterSetName = 'FromKeyVault')]
    [switch]$FromKeyVault
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) was not found on PATH.'
}

$account = az account show --query name -o tsv 2>$null
if (-not $account) {
    throw 'Azure CLI is not logged in. Run az login first.'
}

# Keep in sync with Infrastructure/functions.bicep cosmosdb var (non-secret defaults).
$canonicalDefaults = [ordered]@{
    'cosmosdb__DatabaseId' = 'cultpodcasts-db'
    'cosmosdb__PodcastsContainer' = 'Podcasts'
    'cosmosdb__EpisodesContainer' = 'Episodes'
    'cosmosdb__SubjectsContainer' = 'Subjects'
    'cosmosdb__ActivitiesContainer' = 'Activity'
    'cosmosdb__DiscoveryContainer' = 'Discovery'
    'cosmosdb__LookUpsContainer' = 'LookUps'
    'cosmosdb__PushSubscriptionsContainer' = 'PushSubscriptions'
    'cosmosdb__UseGateway' = 'false'
}

$v2ToCanonical = @{
    'cosmosdbv2__Endpoint' = 'cosmosdb__Endpoint'
    'cosmosdbv2__AuthKeyOrResourceToken' = 'cosmosdb__AuthKeyOrResourceToken'
    'cosmosdbv2__DatabaseId' = 'cosmosdb__DatabaseId'
    'cosmosdbv2__PodcastsContainer' = 'cosmosdb__PodcastsContainer'
    'cosmosdbv2__EpisodesContainer' = 'cosmosdb__EpisodesContainer'
    'cosmosdbv2__SubjectsContainer' = 'cosmosdb__SubjectsContainer'
    'cosmosdbv2__ActivitiesContainer' = 'cosmosdb__ActivitiesContainer'
    'cosmosdbv2__DiscoveryContainer' = 'cosmosdb__DiscoveryContainer'
    'cosmosdbv2__LookUpsContainer' = 'cosmosdb__LookUpsContainer'
    'cosmosdbv2__PushSubscriptionsContainer' = 'cosmosdb__PushSubscriptionsContainer'
    'cosmosdbv2__UseGateway' = 'cosmosdb__UseGateway'
}

$staleSettingNames = @(
    'cosmosdb__Container'
    'cosmosdb__UseGateWay'
) + @($v2ToCanonical.Keys)

function Get-KeyVaultCosmosSecrets {
    Write-Verbose "Reading Cosmos secrets from Key Vault '$KeyVaultName'..."
    $endpoint = az keyvault secret show `
        --vault-name $KeyVaultName `
        --name 'Cosmosdb-Endpoint-V2' `
        --query value `
        -o tsv
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($endpoint)) {
        throw "Failed to read Key Vault secret 'Cosmosdb-Endpoint-V2' from vault '$KeyVaultName'."
    }

    $authKey = az keyvault secret show `
        --vault-name $KeyVaultName `
        --name 'Cosmosdb-AuthKeyOrResourceToken-V2' `
        --query value `
        -o tsv
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($authKey)) {
        throw "Failed to read Key Vault secret 'Cosmosdb-AuthKeyOrResourceToken-V2' from vault '$KeyVaultName'."
    }

    return @{
        Endpoint = $endpoint.Trim()
        AuthKeyOrResourceToken = $authKey.Trim()
    }
}

function Get-AppSettingsMap {
    param([string]$AppName)

    $rows = az functionapp config appsettings list `
        --resource-group $ResourceGroup `
        --name $AppName `
        -o json | ConvertFrom-Json

    $map = @{}
    foreach ($row in $rows) {
        $map[$row.name] = $row.value
    }
    return $map
}

function Build-CanonicalSettings {
    param(
        [hashtable]$ExistingSettings,
        [hashtable]$KeyVaultSecrets
    )

    $settings = [ordered]@{}
    foreach ($entry in $canonicalDefaults.GetEnumerator()) {
        $settings[$entry.Key] = $entry.Value
    }

    if ($FromKeyVault) {
        $settings['cosmosdb__Endpoint'] = $KeyVaultSecrets.Endpoint
        $settings['cosmosdb__AuthKeyOrResourceToken'] = $KeyVaultSecrets.AuthKeyOrResourceToken
        return $settings
    }

    foreach ($v2Key in $v2ToCanonical.Keys) {
        $canonicalKey = $v2ToCanonical[$v2Key]
        if ($ExistingSettings.ContainsKey($v2Key) -and -not [string]::IsNullOrWhiteSpace($ExistingSettings[$v2Key])) {
            $settings[$canonicalKey] = $ExistingSettings[$v2Key]
        }
    }

    if (-not $settings['cosmosdb__Endpoint'] -or -not $settings['cosmosdb__AuthKeyOrResourceToken']) {
        throw 'Missing cosmosdbv2__Endpoint or cosmosdbv2__AuthKeyOrResourceToken on function app. Re-run with -FromKeyVault or ensure v2 settings exist.'
    }

    return $settings
}

Write-Host "Azure subscription: $account"
Write-Host "Resource group: $ResourceGroup"
Write-Host "Source: $(if ($FromKeyVault) { 'Key Vault (Cosmosdb-*-V2 secrets)' } else { 'existing cosmosdbv2__* app settings' })"

$keyVaultSecrets = $null
if ($FromKeyVault) {
    $keyVaultSecrets = Get-KeyVaultCosmosSecrets
}

foreach ($app in $FunctionApps) {
    Write-Host "`n=== $app ==="
    $existing = Get-AppSettingsMap -AppName $app
    $settings = Build-CanonicalSettings -ExistingSettings $existing -KeyVaultSecrets $keyVaultSecrets

    $settingsToApply = @(
        foreach ($entry in $settings.GetEnumerator()) {
            "$($entry.Key)=$($entry.Value)"
        }
    )

    Write-Host "Applying $($settingsToApply.Count) canonical cosmosdb__* settings..."
    if ($PSCmdlet.ShouldProcess($app, 'Apply canonical cosmosdb app settings')) {
        az functionapp config appsettings set `
            --resource-group $ResourceGroup `
            --name $app `
            --settings $settingsToApply `
            -o none
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to update app settings for '$app' (exit code $LASTEXITCODE)."
        }
    }

    $stalePresent = @($staleSettingNames | Where-Object { $existing.ContainsKey($_) })
    if ($stalePresent.Count -gt 0) {
        Write-Host "Removing $($stalePresent.Count) stale setting(s): $($stalePresent -join ', ')"
        if ($PSCmdlet.ShouldProcess($app, "Remove stale cosmos settings ($($stalePresent.Count))")) {
            az functionapp config appsettings delete `
                --resource-group $ResourceGroup `
                --name $app `
                --setting-names $stalePresent
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to remove stale app settings for '$app' (exit code $LASTEXITCODE)."
            }
        }
    } else {
        Write-Host 'No stale cosmos settings to remove.'
    }

    az functionapp config appsettings list `
        --resource-group $ResourceGroup `
        --name $app `
        --query "[?starts_with(name, 'cosmosdb')].{name:name,value:value}" `
        -o table
}

Write-Host "`nCosmos DB app settings applied. Each app will restart to pick up changes."
