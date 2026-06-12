[CmdletBinding(SupportsShouldProcess = $true)]

param(

    [string]$ResourceGroup,

    [string]$Suffix = 'infra',

    [string]$AppName,

    [string]$Runtime = 'linux-x64',

    [string]$Configuration = 'Release',

    [string]$StorageAccount,

    [string]$DeploymentContainer,

    [string]$DeploymentBlobName = 'released-package.zip',

    [ValidateSet('FlexBlob', 'FunctionAppDeploy')]

    [string]$DeploymentMode = 'FlexBlob',

    [switch]$NoRestore,

    [switch]$SkipPackaging,

    [switch]$RemoveUnsupportedRunFromPackageSetting

)



. (Join-Path $PSScriptRoot 'Resolve-DeploySettings.ps1')



$jsonPath = Join-Path $PSScriptRoot 'deploy-api.json'

$settings = Resolve-DeploySettings -JsonPath $jsonPath -AppLabel 'Api' -BoundParameters $PSBoundParameters



$deployParams = @{

    FunctionName = 'api'

    ResourceGroup = $settings.ResourceGroup

    AppName = $settings.AppName

    StorageAccount = $settings.StorageAccount

    DeploymentContainer = $settings.DeploymentContainer

}



foreach ($key in $PSBoundParameters.Keys) {

    if ($key -notin @('ResourceGroup', 'AppName', 'StorageAccount', 'DeploymentContainer')) {

        $deployParams[$key] = $PSBoundParameters[$key]

    }

}



& (Join-Path $PSScriptRoot 'deploy-function-local.ps1') @deployParams

if ($LASTEXITCODE) { exit $LASTEXITCODE }

