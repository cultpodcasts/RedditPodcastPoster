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
