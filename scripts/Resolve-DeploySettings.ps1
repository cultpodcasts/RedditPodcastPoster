function Resolve-DeploySettings {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$JsonPath,

        [Parameter(Mandatory = $true)]
        [string]$AppLabel,

        [hashtable]$BoundParameters = @{}
    )

    $resourceGroup = $null
    $appName = $null
    $storageAccount = $null
    $container = $null

    $hasAllExplicit = $BoundParameters.ContainsKey('ResourceGroup') -and
        $BoundParameters.ContainsKey('AppName') -and
        $BoundParameters.ContainsKey('StorageAccount') -and
        $BoundParameters.ContainsKey('DeploymentContainer')

    if ($hasAllExplicit) {
        return @{
            ResourceGroup = $BoundParameters['ResourceGroup']
            AppName = $BoundParameters['AppName']
            StorageAccount = $BoundParameters['StorageAccount']
            DeploymentContainer = $BoundParameters['DeploymentContainer']
        }
    }

    if ($BoundParameters.ContainsKey('ResourceGroup')) { $resourceGroup = $BoundParameters['ResourceGroup'] }
    if ($BoundParameters.ContainsKey('AppName')) { $appName = $BoundParameters['AppName'] }
    if ($BoundParameters.ContainsKey('StorageAccount')) { $storageAccount = $BoundParameters['StorageAccount'] }
    if ($BoundParameters.ContainsKey('DeploymentContainer')) { $container = $BoundParameters['DeploymentContainer'] }

    $usePrevious = $false
    if (Test-Path $JsonPath) {
        Write-Host "Loading previous deployment details from $JsonPath..."
        $details = Get-Content $JsonPath -Raw | ConvertFrom-Json
        Write-Host 'Loaded:'
        Write-Host "Resource Group: $($details.resourceGroup)"
        Write-Host "App Service: $($details.appName)"
        Write-Host "Storage Account: $($details.storageAccount)"
        Write-Host "Blob Container: $($details.container)"
        $response = Read-Host 'Use these details? (Y/n)'
        if ($response -ne 'n' -and $response -ne 'N') {
            $resourceGroup = $details.resourceGroup
            $appName = $details.appName
            $storageAccount = $details.storageAccount
            $container = $details.container
            $usePrevious = $true
        }
    }

    if (-not $usePrevious) {
        if (-not $resourceGroup) {
            Write-Host 'Fetching Azure resource groups...'
            $resourceGroups = az group list --query '[].name' -o tsv
            $resourceGroups | ForEach-Object { Write-Host $_ }
            $resourceGroup = Read-Host 'Enter the resource group name from the list above'
        }

        while (-not $appName) {
            Write-Host "Fetching Web Apps and Function Apps in resource group $resourceGroup..."
            $webApps = az webapp list --resource-group $resourceGroup --query '[].name' -o tsv
            $functionApps = az functionapp list --resource-group $resourceGroup --query '[].name' -o tsv
            $allApps = @()
            if ($webApps) { $allApps += $webApps }
            if ($functionApps) { $allApps += $functionApps }
            $allApps = $allApps | Sort-Object -Unique
            if (-not $allApps) {
                Write-Host "No Web Apps or Function Apps found in $resourceGroup."
                $tryAgain = Read-Host 'Enter a different resource group? (Y/n)'
                if ($tryAgain -eq 'n' -or $tryAgain -eq 'N') { exit 1 }
                Write-Host 'Fetching Azure resource groups...'
                $resourceGroups = az group list --query '[].name' -o tsv
                $resourceGroups | ForEach-Object { Write-Host $_ }
                $resourceGroup = Read-Host 'Enter the resource group name from the list above'
            } else {
                $allApps | ForEach-Object { Write-Host $_ }
                $appName = Read-Host "Enter the App Service or Function App name for $AppLabel from the list above"
            }
        }

        if (-not $storageAccount) {
            Write-Host "Fetching Storage Accounts in resource group $resourceGroup..."
            $storageAccounts = az storage account list --resource-group $resourceGroup --query '[].name' -o tsv
            $storageAccounts | ForEach-Object { Write-Host $_ }
            $storageAccount = Read-Host 'Enter the Storage Account name from the list above'
        }

        if (-not $container) {
            Write-Host "Fetching Blob Containers in storage account $storageAccount..."
            $containerList = az storage container list --account-name $storageAccount --query '[].name' -o tsv
            $containerList | ForEach-Object { Write-Host $_ }
            $container = Read-Host 'Enter the Blob Container name from the list above'
        }

        @{
            resourceGroup = $resourceGroup
            appName = $appName
            storageAccount = $storageAccount
            container = $container
        } | ConvertTo-Json | Set-Content $JsonPath
    }

    return @{
        ResourceGroup = $resourceGroup
        AppName = $appName
        StorageAccount = $storageAccount
        DeploymentContainer = $container
    }
}
