[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$ResourceGroup = 'AutomatedInfra',

    [string]$StorageAccount = 'cultpodcastsstg',

    [string[]]$ContainerNames = @(
        'api-deployment',
        'discovery-deployment',
        'indexer-deployment',
        'discovery-models'
    )
)

$ErrorActionPreference = 'Stop'

# Mirrors Infrastructure/function-storage.bicep + deploy.yml "Storage (Deploy Bicep)" step.
# Idempotent: creates missing containers only; does not create or modify the storage account.

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) was not found on PATH.'
}

$account = az account show --query name -o tsv 2>$null
if (-not $account) {
    throw 'Azure CLI is not logged in. Run az login first.'
}

Write-Host "Azure subscription: $account"
Write-Host "Resource group: $ResourceGroup"
Write-Host "Storage account: $StorageAccount"
Write-Host "Containers (from function-storage.bicep defaults):"

$storageExists = az storage account show `
    --resource-group $ResourceGroup `
    --name $StorageAccount `
    --query name -o tsv 2>$null

if (-not $storageExists) {
    throw "Storage account '$StorageAccount' was not found in resource group '$ResourceGroup'."
}

foreach ($containerName in $ContainerNames) {
    Write-Host "  - $containerName"
}

foreach ($containerName in $ContainerNames) {
    $exists = az storage container exists `
        --account-name $StorageAccount `
        --name $containerName `
        --auth-mode login `
        --query exists -o tsv

    if ($exists -eq 'true') {
        Write-Host "Container '$containerName' already exists; skipping."
        continue
    }

    if ($PSCmdlet.ShouldProcess("$StorageAccount/$containerName", 'Create blob container')) {
        Write-Host "Creating container '$containerName'..."
        az storage container create `
            --account-name $StorageAccount `
            --name $containerName `
            --auth-mode login `
            --public-access off `
            --only-show-errors | Out-Null

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create container '$containerName' (exit code $LASTEXITCODE)."
        }
    }
}

Write-Host "Storage containers provisioned."
