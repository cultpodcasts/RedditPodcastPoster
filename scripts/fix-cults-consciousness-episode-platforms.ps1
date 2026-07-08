# Repairs Cults to Consciousness episode platform IDs after Apple duration-snipe mis-merge.
# Data-plane Cosmos REST (az cosmosdb is control-plane only).
[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'
$SubscriptionId = 'a6b8f1a2-6163-41bc-aa6d-e33928939a6e'
$ResourceGroup = 'AutomatedData'
$AccountName = 'cultpodcasts-db'
$DatabaseName = 'cultpodcasts-db'
$PodcastId = '1aa72d3d-f1e4-458f-a172-62990ef6c200'

function New-CosmosDbAuthorizationHeader {
    param([string]$Verb, [string]$ResourceType, [string]$ResourceId, [string]$Key, [datetime]$Date)
    $keyBytes = [Convert]::FromBase64String($Key)
    $payload = "$($Verb.ToLowerInvariant())`n$($ResourceType.ToLowerInvariant())`n$ResourceId`n$($Date.ToString('r').ToLowerInvariant())`n`n"
    $hmacSha256 = New-Object System.Security.Cryptography.HMACSHA256
    $hmacSha256.Key = $keyBytes
    [uri]::EscapeDataString("type=master&ver=1.0&sig=$([Convert]::ToBase64String($hmacSha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($payload))))")
}

function Invoke-CosmosDbQuery {
    param([string]$Endpoint, [string]$Key, [string]$ContainerName, [string]$QueryText, [hashtable]$Parameters = @{})
    $resourceId = "dbs/$DatabaseName/colls/$ContainerName"
    $uri = "$Endpoint$resourceId/docs"
    $date = [datetime]::UtcNow
    $auth = New-CosmosDbAuthorizationHeader -Verb POST -ResourceType docs -ResourceId $resourceId -Key $Key -Date $date
    $body = @{ query = $QueryText; parameters = @($Parameters.GetEnumerator() | ForEach-Object { @{ name = $_.Key; value = $_.Value } }) } | ConvertTo-Json -Depth 5 -Compress
    $headers = @{
        Authorization                                = $auth
        'x-ms-date'                                  = $date.ToString('r')
        'x-ms-version'                               = '2018-12-31'
        'x-ms-documentdb-isquery'                      = 'True'
        'Content-Type'                               = 'application/query+json'
        'x-ms-documentdb-query-enablecrosspartition' = 'True'
    }
    (Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Body $body).Documents
}

function Invoke-CosmosDbReplace {
    param([string]$Endpoint, [string]$Key, [string]$ContainerName, [object]$Document, [string]$PartitionKeyValue)
    $resourceId = "dbs/$DatabaseName/colls/$ContainerName/docs/$($Document.id)"
    $uri = "$Endpoint$resourceId"
    $date = [datetime]::UtcNow
    $auth = New-CosmosDbAuthorizationHeader -Verb PUT -ResourceType docs -ResourceId $resourceId -Key $Key -Date $date
    $body = $Document | ConvertTo-Json -Depth 20 -Compress
    $headers = @{
        Authorization                      = $auth
        'x-ms-date'                        = $date.ToString('r')
        'x-ms-version'                     = '2018-12-31'
        'Content-Type'                     = 'application/json'
        'x-ms-documentdb-partitionkey'     = "[`"$PartitionKeyValue`"]"
    }
    Invoke-RestMethod -Method Put -Uri $uri -Headers $headers -Body $body | Out-Null
}

$key = az cosmosdb keys list --subscription $SubscriptionId --resource-group $ResourceGroup --name $AccountName --type keys --query primaryMasterKey -o tsv
$endpoint = "https://$AccountName.documents.azure.com:443/"

$episodeIds = @(
    '43cdd672-5d33-445d-8202-7f1f49e88857'
    '9bdeabe2-8058-450f-b9a7-e0a6fbe09223'
    'd8bfab20-c5d3-4820-ac76-62d52539aa18'
)

$docs = Invoke-CosmosDbQuery -Endpoint $endpoint -Key $key -ContainerName 'Episodes' -QueryText @"
SELECT * FROM c WHERE c.id IN (@e1, @e2, @e3)
"@ -Parameters @{
    '@e1' = $episodeIds[0]
    '@e2' = $episodeIds[1]
    '@e3' = $episodeIds[2]
}

$byId = @{}
foreach ($doc in $docs) { $byId[$doc.id] = $doc }

$sisterWivesAppleId = 1000775796496L
$sisterWivesAppleUrl = 'https://podcasts.apple.com/podcast/growing-up-on-sister-wives-the-dark-side-of-parenting/id1635013492?i=1000775796496'
$mormonAppleId = 1000775957978L
$mormonAppleUrl = 'https://podcasts.apple.com/us/podcast/the-mormon-church-sent-me-to-one-of-the/id1635013492?i=1000775957978'
$mormonSpotifyId = '2imDT8DWgEW7728CfQdGEC'
$mormonSpotifyUrl = 'https://open.spotify.com/episode/2imDT8DWgEW7728CfQdGEC'

# Remove Sister Wives Apple from Buddhist Guru row
$buddhist = $byId['43cdd672-5d33-445d-8202-7f1f49e88857']
$buddhist.PSObject.Properties.Remove('appleId')
if ($null -eq $buddhist.urls) { $buddhist | Add-Member -NotePropertyName urls -NotePropertyValue ([pscustomobject]@{}) }
$buddhist.urls.PSObject.Properties.Remove('apple')

# Add Sister Wives Apple to correct row
$sisterWives = $byId['9bdeabe2-8058-450f-b9a7-e0a6fbe09223']
$sisterWives | Add-Member -NotePropertyName appleId -NotePropertyValue $sisterWivesAppleId -Force
if ($null -eq $sisterWives.urls) { $sisterWives | Add-Member -NotePropertyName urls -NotePropertyValue ([pscustomobject]@{}) }
$sisterWives.urls | Add-Member -NotePropertyName apple -NotePropertyValue $sisterWivesAppleUrl -Force

# Add Mormon Apple + Spotify to YouTube-only row
$mormon = $byId['d8bfab20-c5d3-4820-ac76-62d52539aa18']
$mormon | Add-Member -NotePropertyName appleId -NotePropertyValue $mormonAppleId -Force
$mormon | Add-Member -NotePropertyName spotifyId -NotePropertyValue $mormonSpotifyId -Force
if ($null -eq $mormon.urls) { $mormon | Add-Member -NotePropertyName urls -NotePropertyValue ([pscustomobject]@{}) }
$mormon.urls | Add-Member -NotePropertyName apple -NotePropertyValue $mormonAppleUrl -Force
$mormon.urls | Add-Member -NotePropertyName spotify -NotePropertyValue $mormonSpotifyUrl -Force

$changes = @(
    @{ Id = $buddhist.id; Summary = 'Remove mis-assigned Sister Wives Apple id' }
    @{ Id = $sisterWives.id; Summary = 'Add Sister Wives Apple id 1000775796496' }
    @{ Id = $mormon.id; Summary = 'Add Mormon Apple + Spotify platform ids' }
)

foreach ($change in $changes) {
    $doc = $byId[$change.Id]
    $doc.PSObject.Properties.Remove('_rid')
    $doc.PSObject.Properties.Remove('_self')
    $doc.PSObject.Properties.Remove('_etag')
    $doc.PSObject.Properties.Remove('_attachments')
    $doc.PSObject.Properties.Remove('_ts')
    Write-Host "$($change.Summary) -> $($change.Id)"
    if ($PSCmdlet.ShouldProcess($change.Id, $change.Summary)) {
        Invoke-CosmosDbReplace -Endpoint $endpoint -Key $key -ContainerName 'Episodes' -Document $doc -PartitionKeyValue $PodcastId
    }
}

Write-Host 'Done.'
