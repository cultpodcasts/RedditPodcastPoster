[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('api', 'discover', 'indexer')]
    [string]$FunctionName,

    [string]$ResourceGroup = 'AutomatedInfra',

    [string]$Suffix = 'infra',

    [string]$AppName,

    [string]$Runtime = 'linux-x64',

    [string]$Configuration = 'Release',

    [string]$StorageAccount = 'cultpodcastsstg',

    [string]$DeploymentContainer,

    [string]$DeploymentBlobName = 'released-package.zip',

    [ValidateSet('FlexBlob', 'FunctionAppDeploy')]
    [string]$DeploymentMode = 'FlexBlob',

    [switch]$NoRestore,

    [switch]$SkipPackaging,

    [switch]$RemoveUnsupportedRunFromPackageSetting
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'AzureWebAppDeploy.ps1')

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectMap = @{
    api = 'Cloud/Api/Api.csproj'
    discover = 'Cloud/Discovery/Discovery.csproj'
    indexer = 'Cloud/Indexer/Indexer.csproj'
}
$deploymentContainerMap = @{
    api = 'api-deployment'
    discover = 'discovery-deployment'
    indexer = 'indexer-deployment'
}

if (-not $AppName) {
    $AppName = "$FunctionName-$Suffix"
}

if (-not $DeploymentContainer) {
    $DeploymentContainer = $deploymentContainerMap[$FunctionName]
}

$projectPath = Join-Path $repoRoot $projectMap[$FunctionName]
$artifactsRoot = Join-Path $PSScriptRoot '.deploy-local'
$publishDir = Join-Path $artifactsRoot $FunctionName
$zipPath = Join-Path $artifactsRoot "$FunctionName.zip"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet CLI was not found on PATH.'
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI was not found on PATH.'
}

$account = az account show --query name -o tsv 2>$null
if (-not $account) {
    throw 'Azure CLI is not logged in. Run az login first.'
}

Write-Host "Azure subscription: $account"
Write-Host "Function app: $AppName"
Write-Host "Resource group: $ResourceGroup"
Write-Host "Project: $projectPath"
Write-Host "Storage account: $StorageAccount"
Write-Host "Deployment container: $DeploymentContainer"
Write-Host "Deployment blob: $DeploymentBlobName"
Write-Host "Deployment mode: $DeploymentMode"

$app = az functionapp show --resource-group $ResourceGroup --name $AppName -o json 2>$null | ConvertFrom-Json
if (-not $app) {
    throw "Function app '$AppName' was not found in resource group '$ResourceGroup'."
}

$sku = $app.sku
if (-not $sku -and $app.serverFarmId) {
    $planResourceGroup = ($app.serverFarmId -split '/')[4]
    $planName = ($app.serverFarmId -split '/')[-1]
    $sku = az functionapp plan show --resource-group $planResourceGroup --name $planName --query sku.tier -o tsv
}

if ($sku) {
    $sku = $sku.Trim()
}

Write-Host "SKU: $sku"
$runFromPackage = az functionapp config appsettings list --resource-group $ResourceGroup --name $AppName --query "[?name=='WEBSITE_RUN_FROM_PACKAGE'].value | [0]" -o tsv
if ($DeploymentMode -eq 'FlexBlob' -and $runFromPackage) {
    if (-not $RemoveUnsupportedRunFromPackageSetting) {
        throw "Function app '$AppName' has unsupported app setting WEBSITE_RUN_FROM_PACKAGE=$runFromPackage for Flex blob deployment. Re-run with -RemoveUnsupportedRunFromPackageSetting to remove this stale setting before deployment."
    }

    if ($PSCmdlet.ShouldProcess($AppName, 'Remove unsupported WEBSITE_RUN_FROM_PACKAGE app setting')) {
        az functionapp config appsettings delete --resource-group $ResourceGroup --name $AppName --setting-names WEBSITE_RUN_FROM_PACKAGE
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to remove unsupported WEBSITE_RUN_FROM_PACKAGE app setting with exit code $LASTEXITCODE."
        }
    }
}

if ($SkipPackaging) {
    Write-Host "Skipping publish and package creation. Reusing package $zipPath..."
} else {
    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

    $publishArgs = @('publish', $projectPath, '--configuration', $Configuration, '--runtime', $Runtime, '--output', $publishDir)
    if ($NoRestore) {
        $publishArgs += '--no-restore'
    }

    Write-Host "Publishing $FunctionName..."
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    Write-Host "Creating package $zipPath..."
    New-LinuxFunctionAppZip -SourceDirectory $publishDir -DestinationZip $zipPath
}

if (-not (Test-Path $zipPath)) {
    throw "Package was not created: $zipPath"
}

Write-Host 'Deploying package without changing application settings...'
if ($PSCmdlet.ShouldProcess($AppName, "Deploy $zipPath to production")) {
    if ($DeploymentMode -eq 'FlexBlob') {
        Write-Host "Uploading Flex Consumption package to $StorageAccount/$DeploymentContainer/$DeploymentBlobName..."
        az storage blob upload --account-name $StorageAccount --container-name $DeploymentContainer --file $zipPath --name $DeploymentBlobName --auth-mode login --overwrite
        if ($LASTEXITCODE -ne 0) {
            throw "Azure Blob package upload failed with exit code $LASTEXITCODE."
        }

        Write-Host "Restarting $AppName to activate Flex Consumption package..."
        az functionapp restart --resource-group $ResourceGroup --name $AppName
        if ($LASTEXITCODE -ne 0) {
            throw "Function app restart failed with exit code $LASTEXITCODE."
        }
    } else {
        az functionapp deploy --resource-group $ResourceGroup --name $AppName --src-path $zipPath --type zip
        if ($LASTEXITCODE -ne 0) {
            throw "Azure Functions zip deployment failed with exit code $LASTEXITCODE."
        }
    }
}

Write-Host 'Deployment complete.'
