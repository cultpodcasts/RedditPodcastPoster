# Applies telemetry / cost-reduction app settings directly on Function apps.
# Use when Infrastructure/functions.bicep is not deploying.
# Mirrors vars: jobHostLogging, logging, memoryProbe in functions.bicep

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$ResourceGroup = 'AutomatedInfra',

    [string[]]$FunctionApps = @('indexer-infra', 'discover-infra', 'api-infra')
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) was not found on PATH.'
}

$account = az account show --query name -o tsv 2>$null
if (-not $account) {
    throw 'Azure CLI is not logged in. Run az login first.'
}

# Keep in sync with Infrastructure/functions.bicep (jobHostLogging + logging + memoryProbe).
$settings = @(
    'AzureFunctionsJobHost__Logging__ApplicationInsights__LogLevel__Default=Warning'
    'AzureFunctionsJobHost__Logging__Console__LogLevel__Default=Warning'
    'AzureFunctionsJobHost__Logging__Debug__LogLevel__Default=Warning'
    'AzureFunctionsJobHost__Logging__LogLevel__Default=Warning'
    'Logging__LogLevel__Default=Warning'
    'Logging__LogLevel__Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerHandler=Warning'
    'Logging__LogLevel__Function=Warning'
    'Logging__LogLevel__Azure=Warning'
    'Logging__LogLevel__RedditPodcastPoster=Warning'
    'Logging__LogLevel__Indexer=Information'
    'Logging__LogLevel__Api=Information'
    'Logging__LogLevel__Discovery=Information'
    'Logging__ApplicationInsights__SamplingSettings__IsEnabled=true'
    'Logging__ApplicationInsights__SamplingSettings__ExcludedTypes='
    'Logging__ApplicationInsights__EnableLiveMetricsFilters=true'
    'OTEL_TRACES_SAMPLER=microsoft.fixed_percentage'
    'OTEL_TRACES_SAMPLER_ARG=0.25'
    'APPLICATIONINSIGHTS_SAMPLING_PERCENTAGE=25'
    'memoryProbe__Enabled=false'
)

Write-Host "Azure subscription: $account"
Write-Host "Resource group: $ResourceGroup"
Write-Host "Applying $($settings.Count) telemetry app settings to each function app..."

foreach ($app in $FunctionApps) {
    Write-Host "`n=== $app ==="
    if ($PSCmdlet.ShouldProcess($app, 'Apply telemetry app settings')) {
        az functionapp config appsettings set `
            --resource-group $ResourceGroup `
            --name $app `
            --settings $settings `
            -o none
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to update app settings for '$app' (exit code $LASTEXITCODE)."
        }
    }

    az functionapp config appsettings list `
        --resource-group $ResourceGroup `
        --name $app `
        --query "[?name=='memoryProbe__Enabled' || name=='Logging__LogLevel__RedditPodcastPoster' || name=='OTEL_TRACES_SAMPLER' || name=='OTEL_TRACES_SAMPLER_ARG' || name=='APPLICATIONINSIGHTS_SAMPLING_PERCENTAGE'].{name:name,value:value}" `
        -o table
}

Write-Host "`nTelemetry app settings applied. Each app will restart to pick up changes."
