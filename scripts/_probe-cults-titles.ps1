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
$key = az cosmosdb keys list --subscription $SubscriptionId --resource-group $ResourceGroup --name $AccountName --type keys --query primaryMasterKey -o tsv
$endpoint = "https://$AccountName.documents.azure.com:443/"
$resourceId = "dbs/$DatabaseName/colls/Episodes"
$date = [datetime]::UtcNow
$auth = New-CosmosDbAuthorizationHeader -Verb POST -ResourceType docs -ResourceId $resourceId -Key $key -Date $date
$sql = @'
SELECT c.id, c.title, c.release, c.appleId, c.spotifyId, c.youTubeId
FROM c WHERE c.podcastId = @pid AND (
  CONTAINS(LOWER(c.title), 'sister wives') OR
  CONTAINS(LOWER(c.title), 'mormon church sent') OR
  CONTAINS(LOWER(c.title), 'campus cult') OR
  CONTAINS(LOWER(c.title), 'evil infiltrates campus')
) ORDER BY c.release DESC
'@
$body = @{ query = $sql; parameters = @(@{ name='@pid'; value=$PodcastId }) } | ConvertTo-Json -Depth 5 -Compress
$headers = @{ Authorization = $auth; 'x-ms-date' = $date.ToString('r'); 'x-ms-version' = '2018-12-31'; 'x-ms-documentdb-isquery' = 'True'; 'Content-Type' = 'application/query+json'; 'x-ms-documentdb-query-enablecrosspartition' = 'True' }
(Invoke-RestMethod -Method Post -Uri "$endpoint$resourceId/docs" -Headers $headers -Body $body).Documents | ConvertTo-Json -Depth 6
