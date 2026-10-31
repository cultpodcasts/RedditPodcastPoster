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

function Test-DiscoveryBlob { # pragma: allowlist secret
    param(
        [Parameter(Mandatory = $true)]
        [string]$BlobName
    )

    $exists = az storage blob exists --account-name $StorageAccount --container-name $Container `
        --name $BlobName --auth-mode login --query exists -o tsv
    if ($LASTEXITCODE -ne 0) {
        throw "Could not check whether blob '$BlobName' exists. Nothing was uploaded."
    }

    return ("$exists".Trim() -eq 'true')
}

function Wait-DiscoveryBlobCopy { # pragma: allowlist secret
    param(
        [Parameter(Mandatory = $true)]
        [string]$BlobName
    )

    $deadline = (Get-Date).AddMinutes(10)
    $status = ''
    while ((Get-Date) -lt $deadline) {
        $status = az storage blob show --account-name $StorageAccount --container-name $Container `
            --name $BlobName --auth-mode login --query "properties.copy.status" -o tsv 2>$null
        if ($LASTEXITCODE -eq 0) {
            $status = "$status".Trim()
            if ($status -eq 'success') {
                return
            }

            if ($status -eq 'failed' -or $status -eq 'aborted') {
                throw "Archive copy of '$BlobName' $status. Nothing was uploaded. Re-run, or pass -SkipArchive."
            }
        }

        Start-Sleep -Seconds 2
    }

    throw "Archive copy of '$BlobName' did not finish within 10 minutes (last status: $status). Nothing was uploaded."
}

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
    'vocab.txt',
    'show-accept-rates.csv'
)
$optionalFiles = @('training-metrics.txt')

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

$bundleFiles = $requiredFiles + $optionalFiles

if (-not $SkipArchive) {
    $manifestBlob = "$prefix/discovery-accept.manifest.json" # pragma: allowlist secret
    if (-not (Test-DiscoveryBlob -BlobName $manifestBlob)) { # pragma: allowlist secret
        Write-Host "No live manifest under $prefix/; nothing to archive."
    }
    else {
        $liveManifestPath = Join-Path ([System.IO.Path]::GetTempPath()) "discovery-live-manifest-$([guid]::NewGuid()).json" # pragma: allowlist secret
        az storage blob download `
            --account-name $StorageAccount `
            --container-name $Container `
            --name $manifestBlob `
            --file $liveManifestPath `
            --auth-mode login `
            --only-show-errors -o none
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $liveManifestPath)) {
            throw "Live manifest '$manifestBlob' exists but the download failed. Nothing was uploaded."
        }

        $liveManifest = Get-Content $liveManifestPath -Raw | ConvertFrom-Json
        Remove-Item $liveManifestPath -ErrorAction SilentlyContinue
        # June 2026 bundles carry ModelVersion "1"; add the training time so archive folders stay unique.
        $liveVersion = if ($liveManifest.ModelVersion -match '\.') { $liveManifest.ModelVersion } else {
            "$($liveManifest.ModelVersion).$(([datetime]$liveManifest.TrainedAt).ToUniversalTime().ToString('yyyyMMdd''T''HHmm''Z'''))"
        }
        $archive = "$($ArchivePrefix.Trim().Trim('/'))/$liveVersion"
        Write-Host "Archiving live bundle $prefix/ -> $archive/"
        $archiveDeclined = $false
        foreach ($file in $bundleFiles) {
            $sourceBlob = "$prefix/$file"
            if (-not (Test-DiscoveryBlob -BlobName $sourceBlob)) { # pragma: allowlist secret
                continue
            }

            $destination = "$archive/$file"
            if ($PSCmdlet.ShouldProcess($destination, 'Copy live blob')) {
                az storage blob copy start `
                    --account-name $StorageAccount `
                    --destination-container $Container `
                    --destination-blob $destination `
                    --source-container $Container `
                    --source-blob $sourceBlob `
                    --auth-mode login -o none
                if ($LASTEXITCODE -ne 0) {
                    throw "Failed to start archive copy of $sourceBlob. Nothing was uploaded. Re-run, or pass -SkipArchive."
                }

                Wait-DiscoveryBlobCopy -BlobName $destination # pragma: allowlist secret
            }
            else {
                $archiveDeclined = $true
            }
        }

        if ($archiveDeclined -and -not $WhatIfPreference) {
            throw "Archive copy was not confirmed. Nothing was uploaded."
        }
    }
}

$uploads = [System.Collections.Generic.List[string]]::new()
foreach ($file in $requiredFiles) {
    $uploads.Add($file)
}

foreach ($file in $optionalFiles) {
    if (Test-Path (Join-Path $modelDirectory $file)) {
        $uploads.Add($file)
    }
}

$uploaded = 0
foreach ($file in $uploads) {
    $sourcePath = Join-Path $modelDirectory $file
    $blobName = "$prefix/$file"
    if ($PSCmdlet.ShouldProcess($blobName, 'Upload model blob')) {
        Write-Host "Uploading $blobName ..."
        az storage blob upload `
            --account-name $StorageAccount `
            --container-name $Container `
            --name $blobName `
            --file $sourcePath `
            --auth-mode login `
            --overwrite -o none
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to upload $blobName. The live bundle may be incomplete."
        }

        $uploaded++
    }
}

if ($uploaded -eq $uploads.Count) {
    Write-Host "Discovery scorer model $($newManifest.ModelVersion) uploaded to $StorageAccount/$Container/$prefix/" # pragma: allowlist secret
    Write-Host "Discover picks it up on its next cold start (blob sync on first score)." # pragma: allowlist secret
}
elseif ($uploaded -eq 0) {
    Write-Host "No model blobs were uploaded."
}
else {
    throw "Uploaded $uploaded of $($uploads.Count) model blobs. The live bundle may be incomplete."
}
