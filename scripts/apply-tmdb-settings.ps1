# Applies tmdb__ApiKey on the Function apps when Infrastructure/functions.bicep is not deploying.
# Mirrors the tmdb var in functions.bicep (core settings, all three apps).
# The running app reads the literal app setting. It does not call Key Vault.
#
# Prerequisite: Key Vault secret Tmdb-ApiKey (the TMDB v3 API key).
#   az keyvault secret set --vault-name cultpodcasts-deployment --name Tmdb-ApiKey --value '<key>'
#   .\scripts\apply-tmdb-settings.ps1

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$ResourceGroup = 'AutomatedInfra',

    [string[]]$FunctionApps = @('api-infra', 'discover-infra', 'indexer-infra'),

    [string]$KeyVaultName = 'cultpodcasts-deployment'
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) was not found on PATH.'
}

$account = az account show --query name -o tsv 2>$null
if (-not $account) {
    throw 'Azure CLI is not logged in. Run az login first.'
}

$apiKey = az keyvault secret show --vault-name $KeyVaultName --name 'Tmdb-ApiKey' --query value -o tsv 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($apiKey)) {
    throw "Key Vault secret '$KeyVaultName/Tmdb-ApiKey' is missing. Create a TMDB v3 API key, then: az keyvault secret set --vault-name $KeyVaultName --name Tmdb-ApiKey --value '<key>'"
}

Write-Host "Azure subscription: $account"
Write-Host "Resource group: $ResourceGroup"
Write-Host "Key Vault secret: $KeyVaultName/Tmdb-ApiKey (value not printed)"

foreach ($app in $FunctionApps) {
    Write-Host "Applying tmdb__ApiKey on $app..."
    if ($PSCmdlet.ShouldProcess($app, 'Apply tmdb__ApiKey')) {
        az functionapp config appsettings set `
            --resource-group $ResourceGroup `
            --name $app `
            --settings "tmdb__ApiKey=$apiKey" `
            -o none

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to update app settings for '$app' (exit code $LASTEXITCODE)."
        }
    }

    $name = az functionapp config appsettings list `
        --resource-group $ResourceGroup `
        --name $app `
        --query "[?name=='tmdb__ApiKey'].name | [0]" `
        -o tsv

    if ($name -ne 'tmdb__ApiKey') {
        throw "tmdb__ApiKey was not found on '$app' after apply."
    }

    Write-Host "  $app : tmdb__ApiKey is set"
}

Write-Host "`nTMDB app setting applied. Each Function app restarts to pick up the change."
