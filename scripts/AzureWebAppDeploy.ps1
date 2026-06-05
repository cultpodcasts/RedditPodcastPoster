function Get-KuduDeploymentStatusName {
    param([int]$Status)

    switch ($Status) {
        0 { return 'Pending' }
        1 { return 'Building' }
        2 { return 'Deploying' }
        3 { return 'Failed' }
        4 { return 'Success' }
        default { return "Unknown($Status)" }
    }
}

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

function Wait-KuduDeployment {
    param(
        [Parameter(Mandatory = $true)][string]$AppName,
        [Parameter(Mandatory = $true)][datetime]$NotBeforeUtc,
        [int]$TimeoutSeconds = 600,
        [int]$PollIntervalSeconds = 10
    )

    $deadline = (Get-Date).ToUniversalTime().AddSeconds($TimeoutSeconds)

    while ((Get-Date).ToUniversalTime() -lt $deadline) {
        $latest = Get-LatestKuduDeployment -AppName $AppName
        if (-not $latest) {
            Write-Host "Waiting for Kudu deployment status..."
            Start-Sleep -Seconds $PollIntervalSeconds
            continue
        }

        $received = [datetime]$latest.received_time
        if ($received.ToUniversalTime() -lt $NotBeforeUtc) {
            Write-Host "Waiting for a new deployment to appear in Kudu (latest is from $($latest.received_time))..."
            Start-Sleep -Seconds $PollIntervalSeconds
            continue
        }

        $statusName = Get-KuduDeploymentStatusName -Status $latest.status
        if ($latest.status -eq 4 -and $latest.complete) {
            return @{
                Success = $true
                Deployment = $latest
            }
        }

        if ($latest.status -eq 3) {
            return @{
                Success = $false
                Deployment = $latest
                Reason = "Kudu reported deployment failure (status 3)."
            }
        }

        if ($latest.complete -and $latest.status -ne 4) {
            return @{
                Success = $false
                Deployment = $latest
                Reason = "Kudu marked deployment complete with non-success status $($latest.status) ($statusName)."
            }
        }

        Write-Host "Deployment in progress: status=$($latest.status) ($statusName), complete=$($latest.complete), received=$($latest.received_time)..."
        Start-Sleep -Seconds $PollIntervalSeconds
    }

    return @{
        Success = $false
        Deployment = $latest
        Reason = "Timed out after $TimeoutSeconds seconds waiting for Kudu to report deployment success."
    }
}

function Invoke-WebAppZipDeploy {
    param(
        [Parameter(Mandatory = $true)][string]$ResourceGroup,
        [Parameter(Mandatory = $true)][string]$AppName,
        [Parameter(Mandatory = $true)][string]$ZipFileUrl
    )

    $deployStartedUtc = (Get-Date).ToUniversalTime().AddMinutes(-1)
    Write-Host "Deploying zip package to Azure App Service..."
    Write-Host "Note: Azure CLI may print a JSONDecodeError traceback here even when deployment succeeds; Kudu polling confirms the real outcome."

    $deployOutput = cmd /c "az webapp deploy --resource-group `"$ResourceGroup`" --name `"$AppName`" --src-url `"$ZipFileUrl`" --type zip" 2>&1
    $deployExitCode = $LASTEXITCODE
    $cliJsonParseFailure = $deployOutput -match 'JSONDecodeError|Extra data: line 1 column'

    if ($cliJsonParseFailure) {
        $deployOutput |
            Where-Object { $_ -notmatch 'Traceback|File ".*site-packages|During handling|json\.decoder|requests\.exceptions|knack\.cli|azure/cli' } |
            ForEach-Object { Write-Host $_ }
    }
    else {
        $deployOutput | ForEach-Object { Write-Host $_ }
    }
    if ($deployExitCode -eq 0 -and -not $cliJsonParseFailure) {
        Write-Host "Azure CLI reported deployment initiated successfully."
    }
    elseif ($cliJsonParseFailure) {
        Write-Warning "Azure CLI failed parsing the OneDeploy response. Confirming outcome via Kudu..."
    }
    else {
        Write-Warning "az webapp deploy exited with code $deployExitCode. Confirming outcome via Kudu..."
    }

    Write-Host "Polling Kudu for deployment completion (up to 10 minutes)..."
    $result = Wait-KuduDeployment -AppName $AppName -NotBeforeUtc $deployStartedUtc

    if ($result.Success) {
        $deployment = $result.Deployment
        Write-Host "Deployment succeeded on Azure (Kudu status=$($deployment.status) (Success), ended $($deployment.end_time))."
        if ($deployment.log_url) {
            Write-Host "Deployment log: $($deployment.log_url)"
        }

        return
    }

    $deployment = $result.Deployment
    $status = if ($deployment) { $deployment.status } else { 'unknown' }
    $statusName = if ($deployment) { Get-KuduDeploymentStatusName -Status $deployment.status } else { 'unknown' }
    $complete = if ($deployment) { $deployment.complete } else { 'unknown' }
    $logUrl = if ($deployment) { $deployment.log_url } else { 'n/a' }
    $reason = if ($result.Reason) { $result.Reason } else { 'Deployment could not be confirmed.' }

    Write-Error "$reason Kudu status=$status ($statusName), complete=$complete. Log: $logUrl"
    exit 1
}
