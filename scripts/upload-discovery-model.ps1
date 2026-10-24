[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$ModelDirectory,

    [string]$StorageAccount = 'cultpodcastsstg',

    [string]$Container = 'discovery-models',

    [string]$Prefix = 'current',

    # Folder under which the bundle being replaced is copied first, named after its manifest ModelVersion.
    [string]$ArchivePrefix = 'archive',

    [switch]$SkipArchive
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

$newManifest = Get-Content (Join-Path $modelDirectory 'discovery-accept.manifest.json') -Raw | ConvertFrom-Json
Write-Host "New model version: $($newManifest.ModelVersion) (trained $($newManifest.TrainedAt))"

$bundleFiles = $requiredFiles + @('show-accept-rates.csv', 'training-metrics.txt')

if (-not $SkipArchive) {
    $liveManifestPath = Join-Path ([System.IO.Path]::GetTempPath()) "discovery-live-manifest-$([guid]::NewGuid()).json"
    az storage blob download `
        --account-name $StorageAccount `
        --container-name $Container `
        --name "$prefix/discovery-accept.manifest.json" `
        --file $liveManifestPath `
        --auth-mode login `
        --only-show-errors -o none 2>$null
    if ($LASTEXITCODE -eq 0 -and (Test-Path $liveManifestPath)) {
        $liveManifest = Get-Content $liveManifestPath -Raw | ConvertFrom-Json
        Remove-Item $liveManifestPath -ErrorAction SilentlyContinue
        # June 2026 bundles carry ModelVersion "1"; add the training time so archive folders stay unique.
        $liveVersion = if ($liveManifest.ModelVersion -match '\.') { $liveManifest.ModelVersion } else {
            "$($liveManifest.ModelVersion).$(([datetime]$liveManifest.TrainedAt).ToUniversalTime().ToString('yyyyMMdd''T''HHmm''Z'''))"
        }
        $archive = "$($ArchivePrefix.Trim().Trim('/'))/$liveVersion"
        Write-Host "Archiving live bundle $prefix/ -> $archive/"
        foreach ($file in $bundleFiles) {
            $exists = az storage blob exists --account-name $StorageAccount --container-name $Container `
                --name "$prefix/$file" --auth-mode login --query exists -o tsv
            if ($exists -ne 'true') {
                continue
            }
            if ($PSCmdlet.ShouldProcess("$archive/$file", 'Copy live blob')) {
                az storage blob copy start `
                    --account-name $StorageAccount `
                    --destination-container $Container `
                    --destination-blob "$archive/$file" `
                    --source-container $Container `
                    --source-blob "$prefix/$file" `
                    --auth-mode login -o none
                if ($LASTEXITCODE -ne 0) {
                    throw "Failed to archive $prefix/$file. Nothing uploaded. Re-run, or pass -SkipArchive."
                }
            }
        }
    }
    else {
        Write-Host "No live manifest under $prefix/; nothing to archive."
    }
}

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

$optionalFiles = @('show-accept-rates.csv', 'training-metrics.txt')
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

Write-Host "Discovery scorer model $($newManifest.ModelVersion) uploaded to $StorageAccount/$Container/$prefix/"
Write-Host "Discover picks it up on its next cold start (blob sync on first score)."
