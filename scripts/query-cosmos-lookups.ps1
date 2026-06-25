# Query Cosmos DB LookUps (and optional Podcasts/Episodes) for indexing investigation.
# Uses data-plane REST API — Azure CLI has no `az cosmosdb sql query` command.

[CmdletBinding()]
param(
    [ValidateSet('QuotaReport', 'IndexerKeyState', 'Podcast', 'Episodes')]
    [string]$Query = 'QuotaReport',

    [string]$SubscriptionId = 'a6b8f1a2-6163-41bc-aa6d-e33928939a6e',

    [string]$ResourceGroup = 'AutomatedData',

    [string]$AccountName = 'cultpodcasts-db',

    [string]$DatabaseName = 'cultpodcasts-db',

    [string]$LookUpsContainer = 'LookUps',

    [string]$ReportDate = '',

    [string]$SourceApplication = 'Indexer',

    [string]$PodcastId = '8a0c0f4e-79e0-4d87-bcd5-2156fc0d2f9e'
)

$ErrorActionPreference = 'Stop'

function New-CosmosDbAuthorizationHeader {
    param(
        [string]$Verb,
        [string]$ResourceType,
        [string]$ResourceId,
        [string]$Key,
        [datetime]$Date
    )

    $keyBytes = [Convert]::FromBase64String($Key)
    $payload = "$($Verb.ToLowerInvariant())`n$($ResourceType.ToLowerInvariant())`n$ResourceId`n$($Date.ToString('r').ToLowerInvariant())`n`n"
    $hmacSha256 = New-Object System.Security.Cryptography.HMACSHA256
    $hmacSha256.Key = $keyBytes
    $hash = $hmacSha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($payload))
    $signature = [Convert]::ToBase64String($hash)
    [uri]::EscapeDataString("type=master&ver=1.0&sig=$signature")
}

function Get-YouTubeQuotaReportId {
    param(
        [string]$ReportDate,
        [string]$SourceApplication
    )

    $yyyyMMdd = ($ReportDate -replace '-', '')
    $input = '{0}:{1}' -f $yyyyMMdd, $SourceApplication
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    $hash = $sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($input))
    $guidBytes = New-Object 'System.Byte[]' 16
    [Array]::Copy($hash, 0, $guidBytes, 0, 16)
    $guidBytes[6] = [byte](($guidBytes[6] -band 0x0F) -bor 0x40)
    $guidBytes[8] = [byte](($guidBytes[8] -band 0x3F) -bor 0x80)
    return ([Guid]$guidBytes).ToString()
}

function Invoke-CosmosDbReadDocument {
    param(
        [string]$Endpoint,
        [string]$Key,
        [string]$DatabaseName,
        [string]$ContainerName,
        [string]$DocumentId,
        [string]$PartitionKey
    )

    $resourceId = "dbs/$DatabaseName/colls/$ContainerName/docs/$DocumentId"
    $uri = "$Endpoint$resourceId"
    $date = [datetime]::UtcNow
    $auth = New-CosmosDbAuthorizationHeader -Verb GET -ResourceType docs -ResourceId $resourceId -Key $Key -Date $date

    $headers = @{
        Authorization                      = $auth
        'x-ms-date'                        = $date.ToString('r')
        'x-ms-version'                     = '2018-12-31'
        'x-ms-documentdb-partitionkey'     = "[`"$PartitionKey`"]"
    }

    Invoke-RestMethod -Method Get -Uri $uri -Headers $headers
}

function Invoke-CosmosDbQuery {
    param(
        [string]$Endpoint,
        [string]$Key,
        [string]$DatabaseName,
        [string]$ContainerName,
        [string]$QueryText,
        [hashtable]$Parameters = @{}
    )

    $resourceId = "dbs/$DatabaseName/colls/$ContainerName"
    $uri = "$Endpoint$resourceId/docs"
    $date = [datetime]::UtcNow
    $auth = New-CosmosDbAuthorizationHeader -Verb POST -ResourceType docs -ResourceId $resourceId -Key $Key -Date $date

    $body = @{
        query     = $QueryText
        parameters = @(
            foreach ($name in $Parameters.Keys) {
                @{ name = $name; value = $Parameters[$name] }
            }
        )
    } | ConvertTo-Json -Depth 5 -Compress

    $headers = @{
        Authorization               = $auth
        'x-ms-date'                 = $date.ToString('r')
        'x-ms-version'              = '2018-12-31'
        'x-ms-documentdb-isquery'   = 'True'
        'Content-Type'              = 'application/query+json'
        'x-ms-documentdb-query-enablecrosspartition' = 'True'
        'x-ms-max-item-count'       = '100'
    }

    Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Body $body
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) was not found on PATH.'
}

$account = az account show --query name -o tsv 2>$null
if (-not $account) {
    throw 'Azure CLI is not logged in. Run: az login'
}

$key = az cosmosdb keys list `
    --subscription $SubscriptionId `
    --resource-group $ResourceGroup `
    --name $AccountName `
    --type keys `
    --query primaryMasterKey `
    -o tsv

if (-not $key) {
    throw "Could not read primary key for Cosmos account '$AccountName' in '$ResourceGroup'."
}

$endpoint = "https://$AccountName.documents.azure.com:443/"

switch ($Query) {
    'QuotaReport' {
        if (-not $ReportDate) {
            $ReportDate = (Get-Date).ToUniversalTime().AddDays(-1).ToString('yyyy-MM-dd')
        }

        $reportId = Get-YouTubeQuotaReportId -ReportDate $ReportDate -SourceApplication $SourceApplication

        Write-Host "Cosmos: $AccountName / $DatabaseName / $LookUpsContainer"
        Write-Host "Read: YouTubeQuotaReport id=$reportId reportDate=$ReportDate sourceApplication=$SourceApplication"
        Write-Host ''

        Invoke-CosmosDbReadDocument `
            -Endpoint $endpoint `
            -Key $key `
            -DatabaseName $DatabaseName `
            -ContainerName $LookUpsContainer `
            -DocumentId $reportId `
            -PartitionKey $reportId | ConvertTo-Json -Depth 10
    }

    'IndexerKeyState' {
        $documentId = 'a7c3e1f4-9b2d-4f6a-8c5e-1d3f7a9b2c4e'

        Write-Host "Cosmos: $AccountName / $DatabaseName / $LookUpsContainer"
        Write-Host "Read: YouTubeIndexerKeyState id=$documentId"
        Write-Host ''

        Invoke-CosmosDbReadDocument `
            -Endpoint $endpoint `
            -Key $key `
            -DatabaseName $DatabaseName `
            -ContainerName $LookUpsContainer `
            -DocumentId $documentId `
            -PartitionKey $documentId | ConvertTo-Json -Depth 10
    }

    'Podcast' {
        $sql = @"
SELECT c.id, c.name, c.lastIndexed, c.latestReleased, c.releaseAuthority,
       c.youTubeChannelId, c.youTubePlaylistId, c.spotifyId, c.appleId
FROM c
WHERE c.id = @podcastId
"@

        Write-Host "Cosmos: $AccountName / $DatabaseName / Podcasts"
        Write-Host "Query: podcast id=$PodcastId"
        Write-Host ''

        Invoke-CosmosDbQuery `
            -Endpoint $endpoint `
            -Key $key `
            -DatabaseName $DatabaseName `
            -ContainerName 'Podcasts' `
            -QueryText $sql `
            -Parameters @{ '@podcastId' = $PodcastId } | ConvertTo-Json -Depth 10
    }

    'Episodes' {
        $sql = @"
SELECT c.id, c.title, c.release, c.urls
FROM c
WHERE c.podcastId = @podcastId
  AND (CONTAINS(c.urls.youTube, 'hh4MIFHUzRM') OR CONTAINS(c.urls.youTube, 'wuSWvcS2Yfo'))
ORDER BY c.release DESC
"@

        Write-Host "Cosmos: $AccountName / $DatabaseName / Episodes"
        Write-Host "Query: episodes for podcastId=$PodcastId with target YouTube URLs"
        Write-Host ''

        Invoke-CosmosDbQuery `
            -Endpoint $endpoint `
            -Key $key `
            -DatabaseName $DatabaseName `
            -ContainerName 'Episodes' `
            -QueryText $sql `
            -Parameters @{ '@podcastId' = $PodcastId } | ConvertTo-Json -Depth 10
    }
}
