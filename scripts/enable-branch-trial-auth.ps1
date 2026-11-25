# Lets a non-main branch deploy to a trial resource group.
# Run this locally, signed in as an owner of the Deployment managed identity
# (Management/Deployment) and as User Access Administrator (or Owner) on the subscription.
# The GitHub Actions identity cannot do either step itself:
#   - federated credentials are an Entra app change (a workflow that added its own
#     branch would be able to trust any ref)
#   - User Access Administrator on a new group requires roleAssignments/write, which
#     subscription Contributor does not include
# The subject is the name-based claim this repository still sends. Do not opt the
# repository into immutable subject claims here; that would also change the subject
# main sends, and the existing main credential would stop matching.

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [string]$Branch,

    [string]$Location = 'uksouth',

    [string]$SubscriptionId = 'a6b8f1a2-6163-41bc-aa6d-e33928939a6e',

    [string]$Repository = 'cultpodcasts/RedditPodcastPoster',

    # User-assigned managed identity used by deploy.yml Azure Login. Federated
    # credentials live on this identity, not on an app registration.
    # Empty uses Infrastructure/deployment-principal.json (the workflow preflight reads the same file).
    [string]$AssigneeObjectId,

    [string]$IdentityName = 'Deployment',

    [string]$IdentityResourceGroup = 'Management'
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) was not found on PATH.'
}

if ([string]::IsNullOrWhiteSpace($Branch)) {
    $Branch = (git rev-parse --abbrev-ref HEAD).Trim()
}

if ([string]::IsNullOrWhiteSpace($Branch) -or $Branch -eq 'HEAD' -or $Branch -eq 'main') {
    throw "Branch must be a named branch other than main. Got '$Branch'."
}

if ($Branch -match '\s') {
    throw "Branch name '$Branch' cannot contain whitespace."
}

# Same production groups the workflow guard refuses: AutomatedInfra, Management
# (the Deployment identity's group), and the Cosmos group AutomatedData
# (DATABASE_RESOURCEGROUP_NAME). Refuse before any Azure write.
$productionGroups = @(
    'AutomatedInfra',
    'Management',
    'AutomatedData',
    $IdentityResourceGroup
)
if ([string]::IsNullOrWhiteSpace($ResourceGroup) -or $ResourceGroup -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]{0,89}$') {
    throw "ResourceGroup '$ResourceGroup' must match ^[A-Za-z0-9][A-Za-z0-9_-]{0,89}$ and must not be empty."
}
foreach ($denied in $productionGroups) {
    if ([string]::IsNullOrWhiteSpace($denied)) { continue }
    if ($ResourceGroup.Equals($denied.Trim(), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "ResourceGroup '$ResourceGroup' is a production group (AutomatedInfra, Management, or the Cosmos group AutomatedData). Refusing to create it or grant User Access Administrator."
    }
}

$principalPath = Join-Path $PSScriptRoot '../Infrastructure/deployment-principal.json'
if (-not (Test-Path -LiteralPath $principalPath)) {
    throw "Deployment principal file not found: $principalPath"
}
$principal = Get-Content -LiteralPath $principalPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($AssigneeObjectId)) {
    $AssigneeObjectId = [string]$principal.assigneeObjectId
}
if ($AssigneeObjectId -notmatch '^[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$') {
    throw "AssigneeObjectId '$AssigneeObjectId' is not a GUID. The shared source is Infrastructure/deployment-principal.json."
}

$account = az account show --query id -o tsv 2>$null
if (-not $account) {
    throw 'Azure CLI is not logged in. Run az login first.'
}

if ($account -ne $SubscriptionId) {
    throw "Azure CLI subscription is '$account'. Expected '$SubscriptionId'."
}

$subject = "repo:${Repository}:ref:refs/heads/${Branch}"
$scope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup"
$credentialName = 'gh-branch-' + ($Branch -replace '[^A-Za-z0-9-]', '-')
if ($credentialName.Length -gt 120) {
    $credentialName = $credentialName.Substring(0, 120)
}

if ($PSCmdlet.ShouldProcess($ResourceGroup, "Create resource group in $Location")) {
    az group create --name $ResourceGroup --location $Location --output none
    if ($LASTEXITCODE -ne 0) { throw "az group create failed for '$ResourceGroup'." }
}

$existing = az identity federated-credential list `
    --identity-name $IdentityName `
    --resource-group $IdentityResourceGroup `
    --query "[?subject=='$subject'].name" -o tsv
if ($LASTEXITCODE -ne 0) { throw "Could not list federated credentials on $IdentityName." }
if ($existing) {
    Write-Host "Federated credential already trusts $subject ($existing)."
}
elseif ($PSCmdlet.ShouldProcess("$IdentityResourceGroup/$IdentityName", "Add federated credential $credentialName")) {
    az identity federated-credential create `
        --identity-name $IdentityName `
        --resource-group $IdentityResourceGroup `
        --name $credentialName `
        --issuer 'https://token.actions.githubusercontent.com' `
        --subject $subject `
        --audience 'api://AzureADTokenExchange' `
        --output none
    if ($LASTEXITCODE -ne 0) { throw "az identity federated-credential create failed for '$subject'." }
    Write-Host "Federated credential $credentialName trusts $subject."
}

$assignedIds = @(az role assignment list --assignee $AssigneeObjectId --role 'User Access Administrator' --scope $scope --query '[].id' -o tsv | Where-Object { $_ })
if ($LASTEXITCODE -ne 0) { throw "Could not list role assignments on $scope." }
if ($assignedIds.Count -gt 0) {
    Write-Host "User Access Administrator is already assigned on $scope."
}
elseif ($PSCmdlet.ShouldProcess($scope, 'Assign User Access Administrator to Deployment')) {
    az role assignment create `
        --assignee $AssigneeObjectId `
        --role 'User Access Administrator' `
        --scope $scope `
        --output none
    if ($LASTEXITCODE -ne 0) { throw "az role assignment create failed for '$scope'." }
    Write-Host "User Access Administrator assigned on $scope."
}
