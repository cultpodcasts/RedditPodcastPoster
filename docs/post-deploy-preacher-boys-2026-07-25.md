# Post-deploy Cosmos apply — Preacher Boys (2026-07-25)

**Why:** Prod Cosmospodcast docs are rewritten when the indexer `Save()`s a podcast.
Until `youTubePlaylistOrder` / `youTubePlaylistIdHistory` ship in a deployed build, a Save can
**drop** those unknown properties. Playlist id / expensive flag / publication offset can also be
overwritten by older code or by a sticky probe. Re-apply this checklist **after**
deploying the PR that adds `PlaylistOrder` / Arbitrary walks / playlist-id history
([#920](https://github.com/cultpodcasts/RedditPodcastPoster/pull/920) or successor).

| | |
|-|-|
| Podcast id | `4672c845-15b4-4f88-bbff-567d521fe4a2` |
| Name / fileKey | Preacher Boys Podcast / `preacher_boys_podcast` |
| Account / DB / container | `cultpodcasts-db` / `cultpodcasts-db` / `Podcasts` |

## Desired end state

| Field | Desired | Notes |
|-------|---------|-------|
| `youTubePlaylistId` | `PLKdUWzQpByAQ` | Public show playlist (contains recent episodes). Replaces unlisted `PL3bVHY_fIafVIG-QBn_UtBqpeRfLeBhsJ`. |
| `youTubePlaylistIdHistory` | `[{ id: PL3bVHY_fIafVIG-QBn_UtBqpeRfLeBhsJ, replacedAt: <UTC of first swap> }]` | Seed the former unlisted id — the 2026-07-25 Cosmos patch did not go through `YouTubePlaylistIdChange.Apply`. |
| `youTubePlaylistOrder` | `Arbitrary` | Curated; new items may appear at either end. Requires deployed Arbitrary walk code. |
| `youTubePlaylistQueryIsExpensive` | `false` | Meaningless under Arbitrary (probe suppressed); keep false so old builds do not force odd batch sizing. |
| `youTubePublicationOffset` | `0` | Was `−3456000000000` (−4 days). Video and audio publish near-simultaneously for this show; −4 days pulled the YouTube window wrongly. `0` ≡ `TimeSpan.Zero` (same as null). |

Do **not** change `releaseAuthority` (`YouTube`) or `youTubeChannelId` (`UCUnhn-4KoLWr0ffbPjMf-gQ`) unless separately agreed.

## Verify current (read-only)

```powershell
pwsh ./scripts/query-cosmos-lookups.ps1 -Query Podcast -PodcastId 4672c845-15b4-4f88-bbff-567d521fe4a2
```

Or project the fields this checklist cares about (extend the Podcast query / use the
patch script's BEFORE dump below).

## Apply after deploy

Confirm indexer/api blob deploy first ([production-deploy-truth](../.cursor/rules/production-deploy-truth.mdc)), then run:

```powershell
$ErrorActionPreference = 'Stop'
$SubscriptionId = 'a6b8f1a2-6163-41bc-aa6d-e33928939a6e'
$ResourceGroup = 'AutomatedData'
$AccountName = 'cultpodcasts-db'
$DatabaseName = 'cultpodcasts-db'
$PodcastId = '4672c845-15b4-4f88-bbff-567d521fe4a2'
$FormerPlaylistId = 'PL3bVHY_fIafVIG-QBn_UtBqpeRfLeBhsJ'
$ReplacedAt = [datetime]::Parse('2026-07-25T13:30:00Z').ToUniversalTime()

function New-CosmosDbAuthorizationHeader {
    param([string]$Verb,[string]$ResourceType,[string]$ResourceId,[string]$Key,[datetime]$Date)
    $keyBytes = [Convert]::FromBase64String($Key)
    $payload = "$($Verb.ToLowerInvariant())`n$($ResourceType.ToLowerInvariant())`n$ResourceId`n$($Date.ToString('r').ToLowerInvariant())`n`n"
    $hmac = [System.Security.Cryptography.HMACSHA256]::new($keyBytes)
    $sig = [Convert]::ToBase64String($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($payload)))
    [uri]::EscapeDataString("type=master&ver=1.0&sig=$sig")
}

$key = az cosmosdb keys list --subscription $SubscriptionId --resource-group $ResourceGroup --name $AccountName --type keys --query primaryMasterKey -o tsv
$endpoint = "https://$AccountName.documents.azure.com:443/"

# BEFORE
$resourceId = "dbs/$DatabaseName/colls/Podcasts"
$uri = "$endpoint$resourceId/docs"
$date = [datetime]::UtcNow
$auth = New-CosmosDbAuthorizationHeader -Verb POST -ResourceType docs -ResourceId $resourceId -Key $key -Date $date
$queryBody = @{
    query = 'SELECT c.id, c.name, c.youTubePlaylistId, c.youTubePlaylistIdHistory, c.youTubePlaylistOrder, c.youTubePlaylistQueryIsExpensive, c.youTubePublicationOffset FROM c WHERE c.id = @id'
    parameters = @(@{ name = '@id'; value = $PodcastId })
} | ConvertTo-Json -Depth 5 -Compress
$headers = @{
    Authorization = $auth; 'x-ms-date' = $date.ToString('r'); 'x-ms-version' = '2018-12-31'
    'x-ms-documentdb-isquery' = 'True'; 'Content-Type' = 'application/query+json'
    'x-ms-documentdb-query-enablecrosspartition' = 'True'
}
Write-Host 'BEFORE:'; (Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Body $queryBody).Documents | ConvertTo-Json -Depth 6

# PATCH
$docResourceId = "dbs/$DatabaseName/colls/Podcasts/docs/$PodcastId"
$patchUri = "$endpoint$docResourceId"
$date = [datetime]::UtcNow
$auth = New-CosmosDbAuthorizationHeader -Verb PATCH -ResourceType docs -ResourceId $docResourceId -Key $key -Date $date
$history = @(
    @{ id = $FormerPlaylistId; replacedAt = $ReplacedAt.ToString('o') }
)
$patchBody = @{
    operations = @(
        @{ op = 'set'; path = '/youTubePlaylistId'; value = 'PLKdUWzQpByAQ' }
        @{ op = 'set'; path = '/youTubePlaylistIdHistory'; value = $history }
        @{ op = 'set'; path = '/youTubePlaylistOrder'; value = 'Arbitrary' }
        @{ op = 'set'; path = '/youTubePlaylistQueryIsExpensive'; value = $false }
        @{ op = 'set'; path = '/youTubePublicationOffset'; value = 0 }
    )
} | ConvertTo-Json -Depth 6 -Compress
$patchHeaders = @{
    Authorization = $auth; 'x-ms-date' = $date.ToString('r'); 'x-ms-version' = '2018-12-31'
    'Content-Type' = 'application/json_patch+json'
    'x-ms-documentdb-partitionkey' = "[`"$PodcastId`"]"
}
$patched = Invoke-RestMethod -Method Patch -Uri $patchUri -Headers $patchHeaders -Body $patchBody
Write-Host 'AFTER:'
[pscustomobject]@{
    id = $patched.id
    youTubePlaylistId = $patched.youTubePlaylistId
    youTubePlaylistIdHistory = $patched.youTubePlaylistIdHistory
    youTubePlaylistOrder = $patched.youTubePlaylistOrder
    youTubePlaylistQueryIsExpensive = $patched.youTubePlaylistQueryIsExpensive
    youTubePublicationOffset = $patched.youTubePublicationOffset
} | ConvertTo-Json -Depth 6
```

## After apply — smoke

1. Confirm fields match the desired table above (including history entry for the former unlisted id).
2. Run `index.exe` (or wait for hourly) against Preacher Boys; expect discovery path
   `playlist-arbitrary-full-walk` and Kenny Baldwin (`GUo_kOfWuZI`) YouTube merge if still missing.
3. Watch App Insights for `YouTube arbitrary-playlist walk circuit-breaker tripped:`
   (should **not** fire for this ~355-item playlist).

## Status log

| When (UTC) | What | Result |
|------------|------|--------|
| 2026-07-25 ~13:30 | Applied playlist id + `Arbitrary` + expensive=false (offset left −4d; **no history**) | OK — still present when re-read same day |
| *(after deploy)* | Re-apply full desired table including `youTubePublicationOffset=0` + seed `youTubePlaylistIdHistory` | Pending |
