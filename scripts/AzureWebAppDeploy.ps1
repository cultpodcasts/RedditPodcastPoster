function Get-LatestKuduDeployment {
    param([Parameter(Mandatory = $true)][string]$AppName)

    $uri = "https://$AppName.scm.azurewebsites.net/api/deployments/latest"
    try {
        $json = az rest --method GET --uri $uri --resource "https://management.azure.com/" -o json 2>&1
        if ($LASTEXITCODE -ne 0) {
            return $null
        }

        return $json | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function Test-KuduDeploymentSucceeded {
    param(
        [object]$Deployment,
        [datetime]$NotBefore
    )

    if (-not $Deployment) {
        return $false
    }

    # Kudu deployment status: 4 = success
    if ($Deployment.status -ne 4 -or -not $Deployment.complete) {
        return $false
    }

    $received = [datetime]$Deployment.received_time
    return $received -ge $NotBefore
}

function Invoke-WebAppZipDeploy {
    param(
        [Parameter(Mandatory = $true)][string]$ResourceGroup,
        [Parameter(Mandatory = $true)][string]$AppName,
        [Parameter(Mandatory = $true)][string]$ZipFileUrl
    )

    $deployStarted = (Get-Date).ToUniversalTime().AddMinutes(-2)
    Write-Host "Deploying zip package to Azure App Service..."

    $deployOutput = cmd /c "az webapp deploy --resource-group `"$ResourceGroup`" --name `"$AppName`" --src-url `"$ZipFileUrl`" --type zip" 2>&1
    $deployExitCode = $LASTEXITCODE
    $deployOutput | ForEach-Object { Write-Host $_ }

    $cliJsonParseFailure = $deployOutput -match 'JSONDecodeError|Extra data: line 1 column'
    if ($deployExitCode -eq 0 -and -not $cliJsonParseFailure) {
        Write-Host "Deployment complete."
        return
    }

    if ($cliJsonParseFailure) {
        Write-Warning "Azure CLI failed parsing the OneDeploy response (deployment may still have succeeded)."
    }
    else {
        Write-Warning "az webapp deploy exited with code $deployExitCode."
    }

    Write-Host "Verifying deployment via Kudu..."
    Start-Sleep -Seconds 5

    $latest = Get-LatestKuduDeployment -AppName $AppName
    if (Test-KuduDeploymentSucceeded -Deployment $latest -NotBefore $deployStarted) {
        Write-Host "Deployment succeeded on Azure (Kudu status=$($latest.status), ended $($latest.end_time))."
        if ($latest.log_url) {
            Write-Host "Deployment log: $($latest.log_url)"
        }

        return
    }

    $status = if ($latest) { $latest.status } else { 'unknown' }
    $complete = if ($latest) { $latest.complete } else { 'unknown' }
    $logUrl = if ($latest) { $latest.log_url } else { 'n/a' }
    Write-Error "Deployment could not be confirmed. Kudu status=$status, complete=$complete. Log: $logUrl"
    exit 1
}
