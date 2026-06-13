[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$ModelDirectory,

    [string]$StorageAccount = 'cultpodcastsstg',

    [string]$Container = 'discovery-models',

    [string]$Prefix = 'current'
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI was not found on PATH.'
}

$account = az account show --query name -o tsv 2>$null
if (-not $account) {
    throw 'Azure CLI is not logged in. Run az login first.'
}

$modelDirectory = Resolve-Path $ModelDirectory
$requiredFiles = @(
    'discovery-accept.model.zip',
    'discovery-accept.manifest.json',
    'model.onnx',
    'vocab.txt'
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $modelDirectory $file
    if (-not (Test-Path $path)) {
        throw "Required model file not found: $path"
    }
}

$prefix = $Prefix.Trim().Trim('/')
Write-Host "Azure subscription: $account"
Write-Host "Storage account: $StorageAccount"
Write-Host "Container: $Container"
Write-Host "Prefix: $prefix"
Write-Host "Source: $modelDirectory"

foreach ($file in $requiredFiles) {
    $sourcePath = Join-Path $modelDirectory $file
    $blobName = "$prefix/$file"
    Write-Host "Uploading $blobName ..."
    az storage blob upload `
        --account-name $StorageAccount `
        --container-name $Container `
        --name $blobName `
        --file $sourcePath `
        --auth-mode login `
        --overwrite | Out-Null
}

$optionalFiles = @('show-accept-rates.csv')
foreach ($file in $optionalFiles) {
    $sourcePath = Join-Path $modelDirectory $file
    if (-not (Test-Path $sourcePath)) {
        continue
    }

    $blobName = "$prefix/$file"
    Write-Host "Uploading $blobName ..."
    az storage blob upload `
        --account-name $StorageAccount `
        --container-name $Container `
        --name $blobName `
        --file $sourcePath `
        --auth-mode login `
        --overwrite | Out-Null
}

Write-Host "Discovery scorer model uploaded to $StorageAccount/$Container/$prefix/"
