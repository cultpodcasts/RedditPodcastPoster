# Applies Indexer YouTube application DisplayName app settings on Function apps.
# Use when Infrastructure/functions.bicep is not deploying.
# Prefer apply-youtube-keys.ps1 -DisplayNamesOnly for the same behaviour.

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$ResourceGroup = 'AutomatedInfra',

    [string[]]$FunctionApps = @('indexer-infra', 'discover-infra', 'api-infra')
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$params = @{
    ResourceGroup = $ResourceGroup
    FunctionApps  = $FunctionApps
    DisplayNamesOnly = $true
}
if ($PSCmdlet.ShouldProcess($FunctionApps -join ', ', 'Apply Indexer YouTube DisplayName app settings')) {
    & "$scriptDir\apply-youtube-keys.ps1" @params
}
