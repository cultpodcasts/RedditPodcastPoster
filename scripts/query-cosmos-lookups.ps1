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

        $sql = @"
SELECT TOP 1 c.id, c.reportDate, c.sourceApplication,
       c.podcastsNotIndexedDueToQuota, c.podcastsNotEnrichedDueToQuota,
       c.ringExhaustionCount, c.nonQuotaErrorCount,
       c.keys, c.usedIndexerKeys, c.unusedIndexerKeys, c._ts
FROM c
WHERE c.type = 'YouTubeQuotaReport'
  AND c.reportDate = @reportDate
  AND c.sourceApplication = @sourceApplication
ORDER BY c._ts DESC
"@

        Write-Host "Cosmos: $AccountName / $DatabaseName / $LookUpsContainer"
        Write-Host "Query: YouTubeQuotaReport for reportDate=$ReportDate sourceApplication=$SourceApplication"
        Write-Host ''

        Invoke-CosmosDbQuery `
            -Endpoint $endpoint `
            -Key $key `
            -DatabaseName $DatabaseName `
            -ContainerName $LookUpsContainer `
            -QueryText $sql `
            -Parameters @{
                '@reportDate'        = $ReportDate
                '@sourceApplication' = $SourceApplication
            } | ConvertTo-Json -Depth 10
    }

    'IndexerKeyState' {
        $sql = "SELECT TOP 1 * FROM c WHERE c.type = 'YouTubeIndexerKeyState' ORDER BY c._ts DESC"

        Write-Host "Cosmos: $AccountName / $DatabaseName / $LookUpsContainer"
        Write-Host 'Query: YouTubeIndexerKeyState (latest)'
        Write-Host ''

        Invoke-CosmosDbQuery `
            -Endpoint $endpoint `
            -Key $key `
            -DatabaseName $DatabaseName `
            -ContainerName $LookUpsContainer `
            -QueryText $sql | ConvertTo-Json -Depth 10
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
