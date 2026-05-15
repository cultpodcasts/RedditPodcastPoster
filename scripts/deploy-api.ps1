# Deploy Api to Azure App Service using remote-build

$jsonPath = Join-Path $PSScriptRoot "deploy-api.json"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "Cloud/Api/Api.csproj"
$publishDir = Join-Path $PSScriptRoot "publish-api"
$zipName = Join-Path $PSScriptRoot "api.zip"

function Check-StorageRole {
    param($storageAccount)
    Write-Host "Checking for Storage Blob Data Contributor role..."
    $userObjectId = cmd /c az ad signed-in-user show --query id -o tsv
    $subscriptionId = cmd /c az account show --query id -o tsv
    $scope = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Storage/storageAccounts/$storageAccount"
    $roles = cmd /c az role assignment list --assignee "`"$userObjectId`"" --scope "`"$scope`"" --query "[].roleDefinitionName" -o tsv
    if ($roles -notcontains "Storage Blob Data Contributor" -and $roles -notcontains "Storage Blob Data Owner") {
        Write-Error "You must be assigned the 'Storage Blob Data Contributor' or 'Storage Blob Data Owner' role on the storage account $storageAccount."
        exit 1
    }
}

$details = $null
$usePrevious = $false
if (Test-Path $jsonPath) {
    Write-Host "Loading previous deployment details from $jsonPath..."
    $details = Get-Content $jsonPath | ConvertFrom-Json
    Write-Host "Loaded:"
    Write-Host "Resource Group: $($details.resourceGroup)"
    Write-Host "App Service: $($details.appName)"
    Write-Host "Storage Account: $($details.storageAccount)"
    Write-Host "Blob Container: $($details.container)"
    $response = Read-Host "Use these details? (Y/n)"
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
        Write-Host "Fetching Azure resource groups..."
        $resourceGroups = cmd /c az group list --query "[].name" -o tsv
        $resourceGroups | ForEach-Object { Write-Host $_ }
        $resourceGroup = Read-Host "Enter the resource group name from the list above"
    }
    # Loop until a valid app name is selected
    $appName = $null
    while (-not $appName) {
        Write-Host "Fetching Web Apps and Function Apps in resource group $resourceGroup..."
        $webApps = cmd /c az webapp list --resource-group "`"$resourceGroup`"" --query "[].name" -o tsv
        $functionApps = cmd /c az functionapp list --resource-group "`"$resourceGroup`"" --query "[].name" -o tsv
        $allApps = @()
        if ($webApps) { $allApps += $webApps }
        if ($functionApps) { $allApps += $functionApps }
        $allApps = $allApps | Sort-Object -Unique
        if (-not $allApps) {
            Write-Host "No Web Apps or Function Apps found in $resourceGroup."
            $tryAgain = Read-Host "Enter a different resource group? (Y/n)"
            if ($tryAgain -eq 'n' -or $tryAgain -eq 'N') { exit }
            $resourceGroups = cmd /c az group list --query "[].name" -o tsv
            $resourceGroups | ForEach-Object { Write-Host $_ }
            $resourceGroup = Read-Host "Enter the resource group name from the list above"
        } else {
            $allApps | ForEach-Object { Write-Host $_ }
            $appName = Read-Host "Enter the App Service or Function App name for Api from the list above"
        }
    }
    if (-not $storageAccount) {
        Write-Host "Fetching Storage Accounts in resource group $resourceGroup..."
        $storageAccounts = cmd /c az storage account list --resource-group "`"$resourceGroup`"" --query "[].name" -o tsv
        $storageAccounts | ForEach-Object { Write-Host $_ }
        $storageAccount = Read-Host "Enter the Storage Account name from the list above"
    }
    if (-not $container) {
        Write-Host "Fetching Blob Containers in storage account $storageAccount..."
        $containerList = cmd /c az storage container list --account-name "`"$storageAccount`"" --query "[].name" -o tsv
        $containerList | ForEach-Object { Write-Host $_ }
        $container = Read-Host "Enter the Blob Container name from the list above"
    }
    # Save details for next time
    @{
        resourceGroup = $resourceGroup
        appName = $appName
        storageAccount = $storageAccount
        container = $container
    } | ConvertTo-Json | Set-Content $jsonPath
}

Check-StorageRole $storageAccount

# Build and publish the project
Write-Host "Publishing Api project..."
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $projectPath -c Release -r linux-x64 -o $publishDir
if (!(Test-Path $publishDir)) {
    Write-Error "Publish directory not found: $publishDir"
    exit 1
}
if (Test-Path $zipName) { Remove-Item $zipName -Force }
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipName -Force
if (!(Test-Path $zipName)) {
    Write-Error "Zip file not created: $zipName"
    exit 1
}

# Upload zip to Azure Blob Storage (requires az login)
Write-Host "Uploading zip to Azure Blob Storage..."
az storage blob upload --account-name "`"$storageAccount`"" --container-name "`"$container`"" --file "`"$zipName`"" --name "`"$(Split-Path $zipName -Leaf)`"" --auth-mode login --overwrite

# Get the blob SAS URL (update expiry as needed)
$expiry = (Get-Date).AddHours(2).ToString("yyyy-MM-ddTHH:mm:ssZ")
$sasToken = cmd /c az storage blob generate-sas --account-name "`"$storageAccount`"" --container-name "`"$container`"" --name "`"$(Split-Path $zipName -Leaf)`"" --permissions r --expiry "`"$expiry`"" --https-only --auth-mode login --as-user --output tsv
if ($sasToken) {
    $zipFileUrl = "https://$storageAccount.blob.core.windows.net/$container/$(Split-Path $zipName -Leaf)?$sasToken"
} else {
    $zipFileUrl = "https://$storageAccount.blob.core.windows.net/$container/$(Split-Path $zipName -Leaf)"
}

# Deploy the zip package (do NOT update environment variables)
Write-Host "Deploying zip package to Azure App Service..."
cmd /c az webapp deploy --resource-group "$resourceGroup" --name "$appName" --src-url "`"$zipFileUrl`"" --type zip

Write-Host "Deployment complete."
