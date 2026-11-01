#Requires -Version 7
<#
.SYNOPSIS
  Add a GitHub issue to a cultpodcasts Projects V2 board (default: @cultpodcasts features,
  https://github.com/users/cultpodcasts/projects/1) in the Todo column, and verify the card
  is actually listed on the board.

.DESCRIPTION
  Uses the Projects V2 REST API (POST /users/{owner}/projectsV2/{number}/items), not the
  GraphQL addProjectV2ItemById mutation. GraphQL adds have produced "ghost" links on this
  account: the mutation returns an item id and the issue sidebar shows the project, but the
  item never appears in projectV2.items or on the board UI (Sep 2026). The REST write path
  persists the item and its Status field durably.

  If the REST add returns 422 "Content already exists" while the board list does NOT show
  the issue, a GraphQL ghost link is blocking the add: the script deletes that ghost item
  (via GraphQL deleteProjectV2Item) and retries the REST add.

  Success criterion: the issue number appears in the board's item list
  (GET /users/{owner}/projectsV2/{number}/items). A linked-but-unlisted issue is a failure.

.PARAMETER IssueUrl
  Full issue URL, e.g. https://github.com/cultpodcasts/RedditPodcastPoster/issues/989

.PARAMETER Owner
  Project owner login. Default: cultpodcasts

.PARAMETER ProjectNumber
  Project number. Default: 1 (@cultpodcasts features)

.PARAMETER StatusName
  Single-select Status option. Default: Todo

.PARAMETER TimeoutSeconds
  Max time to wait for board-list visibility after add. Default: 300.
  GitHub's list index can lag minutes behind the item write; the item + Status are already
  persisted even while the list lags.

.PARAMETER PollSeconds
  Delay between visibility checks. Default: 15
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $IssueUrl,

    [string] $Owner = 'cultpodcasts',

    [int] $ProjectNumber = 1,

    [string] $StatusName = 'Todo',

    [int] $TimeoutSeconds = 300,

    [int] $PollSeconds = 15
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($IssueUrl -notmatch 'github\.com/([^/]+)/([^/]+)/issues/(\d+)') {
    throw "IssueUrl must look like https://github.com/owner/repo/issues/N — got: $IssueUrl"
}
$repoOwner = $Matches[1]
$repoName = $Matches[2]
$issueNumber = [int]$Matches[3]

$itemsPath = "/users/$Owner/projectsV2/$ProjectNumber/items"

# Runs gh with stdout and stderr merged, then splits them so only stdout is parsed as JSON or
# numbers. Stderr (notices, deprecation lines) is kept for failure messages only.
function Invoke-Gh {
    $merged = & gh @args 2>&1
    $exitCode = $LASTEXITCODE
    $stdout = @($merged | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] } | ForEach-Object { "$_" })
    $stderr = @($merged | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } | ForEach-Object { "$_" })
    return [pscustomobject]@{
        ExitCode = $exitCode
        Stdout   = ($stdout -join "`n")
        Stderr   = ($stderr -join "`n")
        All      = (@($stdout) + @($stderr)) -join "`n"
    }
}

# Lists every issue number on the board. The items endpoint pages at 100, so --paginate is
# required (the board passed 100 items in Oct 2026; #1224). The --jq filter emits one scalar
# per item so paged output concatenates cleanly; draft items have no content.number.
function Get-BoardIssueNumbers {
    $r = Invoke-Gh api --paginate "$($itemsPath)?per_page=100" --jq '.[] | .content.number // empty'
    if ($r.ExitCode -ne 0) { throw "Board list failed: $($r.All)" }
    return @($r.Stdout -split "`n" | Where-Object { $_ -match '^\s*\d+\s*$' } | ForEach-Object { [int]$_.Trim() })
}

function Get-PropertyValue($Object, [string] $Name) {
    if ($null -eq $Object) { return $null }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop) { return $null }
    return $prop.Value
}

# Reads the Status option name from a Projects V2 REST item. Option names are { html, raw }
# objects, so compare .raw. Throws a clear message instead of a StrictMode property error
# when the response has no Status field (for example when fields[] was not requested).
function Get-ItemStatusName($Item, $StatusFieldId) {
    $itemFields = @(Get-PropertyValue $Item 'fields')
    $status = $itemFields | Where-Object {
        $null -ne $_ -and ((Get-PropertyValue $_ 'name') -eq 'Status' -or "$(Get-PropertyValue $_ 'id')" -eq "$StatusFieldId")
    } | Select-Object -First 1
    if ($null -eq $status) {
        throw "Status field missing from response (field id $StatusFieldId); got fields: $(($itemFields | ForEach-Object { Get-PropertyValue $_ 'name' }) -join ', ')."
    }
    $name = Get-PropertyValue (Get-PropertyValue $status 'value') 'name'
    if ($name -is [string]) { return $name }
    return [string](Get-PropertyValue $name 'raw')
}

Write-Host "Resolving $repoOwner/$repoName#$issueNumber …"
$issue = & gh api "/repos/$repoOwner/$repoName/issues/$issueNumber" --jq '{id, node_id, title}' | ConvertFrom-Json

if ((Get-BoardIssueNumbers) -contains $issueNumber) {
    Write-Host "Already visible on the board."
    exit 0
}

# Status field + option ids (REST fields endpoint)
$fields = & gh api "/users/$Owner/projectsV2/$ProjectNumber/fields" | ConvertFrom-Json
$statusField = $fields | Where-Object { $_.name -eq 'Status' -and $_.data_type -eq 'single_select' } | Select-Object -First 1
if (-not $statusField) { throw "No single-select Status field on project $Owner/$ProjectNumber." }
$statusOption = $statusField.options | Where-Object { $_.name.raw -eq $StatusName } | Select-Object -First 1
if (-not $statusOption) {
    $names = ($statusField.options | ForEach-Object { $_.name.raw }) -join ', '
    throw "Status option '$StatusName' not found. Available: $names"
}

function Add-ItemViaRest {
    $r = Invoke-Gh api -X POST $itemsPath -f type=Issue -F id=$($issue.id)
    if ($r.ExitCode -eq 0) {
        return ($r.Stdout | ConvertFrom-Json).id
    }
    if ($r.All -match 'already exists') {
        return $null   # ghost or pre-existing link
    }
    throw "REST add failed: $($r.All)"
}

Write-Host "Adding via REST …"
$itemDbId = Add-ItemViaRest

if (-not $itemDbId) {
    Write-Host "Add blocked by an existing link the board does not show — clearing ghost GraphQL item(s) …"
    $ghostQuery = @'
query($owner: String!, $name: String!, $number: Int!) {
  repository(owner: $owner, name: $name) {
    issue(number: $number) {
      projectItems(first: 20) {
        nodes {
          id
          project { ... on ProjectV2 { id number owner { ... on User { login } ... on Organization { login } } } }
        }
      }
    }
  }
}
'@
    $links = (& gh api graphql -f query=$ghostQuery -f owner=$repoOwner -f name=$repoName -F number=$issueNumber |
        ConvertFrom-Json).data.repository.issue.projectItems.nodes
    $ghosts = @($links | Where-Object { $_.project.number -eq $ProjectNumber -and $_.project.owner.login -eq $Owner })
    if (-not $ghosts) { throw "422 'already exists' but no project link found — cannot recover automatically." }
    foreach ($ghost in $ghosts) {
        $projectNodeId = [string]$ghost.project.id
        $ghostId = [string]$ghost.id
        Write-Host "  deleting ghost item $ghostId"
        & gh api graphql `
            -f query='mutation($projectId:ID!, $itemId:ID!){ deleteProjectV2Item(input:{projectId:$projectId, itemId:$itemId}){ deletedItemId } }' `
            -f projectId=$projectNodeId -f itemId=$ghostId | Out-Null
    }
    Start-Sleep -Seconds 2
    $itemDbId = Add-ItemViaRest
    if (-not $itemDbId) { throw "REST add still reports 'already exists' after ghost cleanup." }
}

Write-Host "Item $itemDbId created. Setting Status=$StatusName …"
$body = @{ fields = @(@{ id = $statusField.id; value = $statusOption.id }) } | ConvertTo-Json -Compress
$patchOut = $body | & gh api -X PATCH "$itemsPath/$itemDbId" --input - 2>&1
if ($LASTEXITCODE -ne 0) { throw "Status PATCH failed for item ${itemDbId}: $($patchOut -join "`n")" }

# Confirm the item itself persisted with the right status (direct GET, index-independent).
# The item GET returns only the Title field unless field ids are requested with fields[] (#1043).
$itemCheckRaw = Invoke-Gh api "$itemsPath/$($itemDbId)?fields[]=$($statusField.id)"
if ($itemCheckRaw.ExitCode -ne 0) { throw "Item GET failed for item ${itemDbId}: $($itemCheckRaw.All)" }
$itemCheck = $itemCheckRaw.Stdout | ConvertFrom-Json
$appliedStatus = Get-ItemStatusName -Item $itemCheck -StatusFieldId $statusField.id
if ($appliedStatus -ne $StatusName) { throw "Status did not persist (got '$appliedStatus')." }
Write-Host "Item persisted with Status=$appliedStatus. Waiting for the board list to index it …"

$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
$attempt = 0
do {
    $attempt++
    if ((Get-BoardIssueNumbers) -contains $issueNumber) {
        Write-Host "OK: #$issueNumber is on https://github.com/users/$Owner/projects/$ProjectNumber ($StatusName) after $attempt check(s)."
        exit 0
    }
    if ([DateTime]::UtcNow -ge $deadline) { break }
    Write-Host "  not indexed yet (check $attempt) — retrying in ${PollSeconds}s"
    Start-Sleep -Seconds $PollSeconds
} while ($true)

Write-Warning @"
Item $itemDbId for #$issueNumber is PERSISTED with Status=$StatusName (direct REST GET confirms it),
but the board list has not indexed it within ${TimeoutSeconds}s. GitHub's Projects index is lagging.
Re-run this script later — it exits 0 immediately once the card is listed.
Issue: $IssueUrl
Board: https://github.com/users/$Owner/projects/$ProjectNumber
"@
exit 2
