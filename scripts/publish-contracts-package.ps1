<#
.SYNOPSIS
  Publishes contracts/ when the generated wire files differ from the latest GitHub Packages version.

.DESCRIPTION
  contracts/package.json records the release line (major.minor). Its patch is 0 in git and is
  not the version that ships. This script hashes the generated files, reads the highest
  published version with npm view, and publishes major.minor.(highest patch + 1) only when
  that hash differs. The hash is stored on the published package as contractsContentHash so
  the next run can skip. Nothing is committed back to git.

  An npm view failure other than "package not found" (E404) fails the run. A retry of a
  publish that already stored this hash exits 0.
#>
[CmdletBinding()]
param(
    [string] $ContractsDirectory,
    [scriptblock] $NpmCommand = { & npm @args }
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$GeneratedRelativePaths = @(
    'src/index.ts',
    'src/zod.ts',
    'schemas/contracts.schema.json'
)

function Get-ContractsContentHash {
    param([Parameter(Mandatory)][string] $Directory)

    $utf8 = New-Object System.Text.UTF8Encoding $false
    $buffer = New-Object System.IO.MemoryStream
    try {
        foreach ($relative in $GeneratedRelativePaths) {
            $path = Join-Path $Directory ($relative -replace '/', [IO.Path]::DirectorySeparatorChar)
            if (-not (Test-Path -LiteralPath $path)) {
                throw "Generated contract file is missing: $relative. Build Tools/ContractExporter before publishing."
            }

            $text = [System.IO.File]::ReadAllText($path).Replace("`r`n", "`n").Replace("`r", "`n")
            $bytes = $utf8.GetBytes("$relative`n$text`n")
            $buffer.Write($bytes, 0, $bytes.Length)
        }

        $buffer.Position = 0
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try {
            $hash = $sha.ComputeHash($buffer)
        }
        finally {
            $sha.Dispose()
        }

        return ([System.BitConverter]::ToString($hash)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $buffer.Dispose()
    }
}

function ConvertFrom-NpmVersionsOutput {
    param([int] $ExitCode, [string] $Output)

    if ($ExitCode -ne 0) {
        if ($Output -match 'E404') {
            return [pscustomobject]@{ Ok = $true; Versions = @() }
        }

        return [pscustomobject]@{ Ok = $false; Versions = @(); Error = $Output }
    }

    $trimmed = $Output.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        return [pscustomobject]@{ Ok = $true; Versions = @() }
    }

    $parsed = $trimmed | ConvertFrom-Json
    return [pscustomobject]@{ Ok = $true; Versions = @($parsed | ForEach-Object { "$_" }) }
}

function ConvertFrom-NpmHashOutput {
    param([int] $ExitCode, [string] $Output)

    if ($ExitCode -ne 0) {
        if ($Output -match 'E404') {
            return [pscustomobject]@{ Ok = $true; Hash = $null }
        }

        return [pscustomobject]@{ Ok = $false; Hash = $null; Error = $Output }
    }

    $trimmed = $Output.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed -eq 'undefined') {
        return [pscustomobject]@{ Ok = $true; Hash = $null }
    }

    if ($trimmed.StartsWith('"')) {
        return [pscustomobject]@{ Ok = $true; Hash = [string]($trimmed | ConvertFrom-Json) }
    }

    return [pscustomobject]@{ Ok = $true; Hash = $trimmed }
}

function Get-ContractsReleaseVersions {
    param([string[]] $Versions)

    $parsed = [System.Collections.Generic.List[version]]::new()
    foreach ($version in @($Versions)) {
        if ($version -match '^\d+\.\d+\.\d+$') {
            $parsed.Add([version] $version)
        }
    }

    return [pscustomobject]@{ Versions = $parsed }
}

function Get-HighestContractVersion {
    param([System.Collections.Generic.List[version]] $Versions)

    $highest = $null
    foreach ($version in $Versions) {
        if ($null -eq $highest -or $version -gt $highest) {
            $highest = $version
        }
    }

    return $highest
}

function Get-NextContractPatch {
    param(
        [System.Collections.Generic.List[version]] $Versions,
        [int] $Major,
        [int] $Minor
    )

    $highestPatch = -1
    foreach ($version in $Versions) {
        if ($version.Major -eq $Major -and $version.Minor -eq $Minor -and $version.Build -gt $highestPatch) {
            $highestPatch = $version.Build
        }
    }

    return $highestPatch + 1
}

function Get-ContractsPublishPlan {
    param(
        [Parameter(Mandatory)][string] $CommittedVersion,
        [Parameter(Mandatory)][string] $ContentHash,
        [string[]] $PublishedVersions = @(),
        [string] $LatestContentHash
    )

    if ($CommittedVersion -notmatch '^(\d+)\.(\d+)\.\d+$') {
        return [pscustomobject]@{
            Action  = 'fail'
            Version = $null
            Reason  = "contracts/package.json version '$CommittedVersion' is not major.minor.patch."
        }
    }

    $committed = [version] $CommittedVersion
    $published = (Get-ContractsReleaseVersions $PublishedVersions).Versions
    if ($published.Count -eq 0) {
        $version = '{0}.{1}.0' -f $committed.Major, $committed.Minor
        return [pscustomobject]@{
            Action  = 'publish'
            Version = $version
            Reason  = "No published release. Publishing $version."
        }
    }

    $latest = Get-HighestContractVersion $published
    if ($committed.Major -lt $latest.Major -or ($committed.Major -eq $latest.Major -and $committed.Minor -lt $latest.Minor)) {
        return [pscustomobject]@{
            Action  = 'fail'
            Version = $null
            Reason  = "Committed release line $($committed.Major).$($committed.Minor) is behind published $($latest.Major).$($latest.Minor)."
        }
    }

    $sameLine = $committed.Major -eq $latest.Major -and $committed.Minor -eq $latest.Minor
    if ($sameLine -and $LatestContentHash -and $LatestContentHash -eq $ContentHash) {
        return [pscustomobject]@{
            Action  = 'skip'
            Version = $latest.ToString()
            Reason  = "Generated contracts match published $($latest.ToString()). Nothing to publish."
        }
    }

    $nextPatch = Get-NextContractPatch -Versions $published -Major $committed.Major -Minor $committed.Minor
    if (-not $sameLine) {
        $nextPatch = 0
    }

    $version = '{0}.{1}.{2}' -f $committed.Major, $committed.Minor, $nextPatch
    $why = if (-not $sameLine) {
        'the release line moved forward'
    } elseif ([string]::IsNullOrWhiteSpace($LatestContentHash)) {
        'the published package has no content hash'
    } else {
        'the generated contracts differ from the published package'
    }

    return [pscustomobject]@{
        Action  = 'publish'
        Version = $version
        Reason  = "Publishing $version because $why."
    }
}

function Set-ContractsContentHash {
    param(
        [Parameter(Mandatory)][string] $PackageJson,
        [Parameter(Mandatory)][string] $ContentHash
    )

    $node = [System.Text.Json.Nodes.JsonNode]::Parse((Get-Content -LiteralPath $PackageJson -Raw))
    $node['contractsContentHash'] = $ContentHash
    $options = [System.Text.Json.JsonSerializerOptions]::new()
    $options.WriteIndented = $true
    $text = $node.ToJsonString($options)
    if (-not $text.EndsWith("`n")) {
        $text += "`n"
    }

    [System.IO.File]::WriteAllText($PackageJson, $text)
}

function Invoke-NpmCapture {
    param(
        [Parameter(Mandatory)][scriptblock] $Command,
        [Parameter(ValueFromRemainingArguments = $true)][string[]] $ArgumentList
    )

    $output = & $Command @ArgumentList 2>&1 | Out-String
    $code = 0
    if (Test-Path -Path variable:LASTEXITCODE) {
        $code = [int] $LASTEXITCODE
    }

    return [pscustomobject]@{ ExitCode = $code; Output = $output }
}

function Invoke-ContractsPackagePublish {
    param(
        [Parameter(Mandatory)][string] $Directory,
        [Parameter(Mandatory)][scriptblock] $Command
    )

    $packageJson = Join-Path $Directory 'package.json'
    $package = [System.Text.Json.JsonDocument]::Parse((Get-Content -LiteralPath $packageJson -Raw)).RootElement
    $name = $package.GetProperty('name').GetString()
    $committed = $package.GetProperty('version').GetString()
    $hash = Get-ContractsContentHash -Directory $Directory

    $versionsResult = Invoke-NpmCapture -Command $Command -- view $name versions --json
    $versions = ConvertFrom-NpmVersionsOutput -ExitCode $versionsResult.ExitCode -Output $versionsResult.Output
    if (-not $versions.Ok) {
        Write-Host "npm view versions failed. Not publishing."
        Write-Host $versionsResult.Output
        return 1
    }

    $latestHash = $null
    $parsed = (Get-ContractsReleaseVersions $versions.Versions).Versions
    if ($parsed.Count -gt 0) {
        $latest = (Get-HighestContractVersion $parsed).ToString()
        $hashResult = Invoke-NpmCapture -Command $Command -- view "$name@$latest" contractsContentHash --json
        $hashView = ConvertFrom-NpmHashOutput -ExitCode $hashResult.ExitCode -Output $hashResult.Output
        if (-not $hashView.Ok) {
            Write-Host "npm view contractsContentHash failed for $latest. Not publishing."
            Write-Host $hashResult.Output
            return 1
        }

        $latestHash = $hashView.Hash
    }

    $plan = Get-ContractsPublishPlan -CommittedVersion $committed -ContentHash $hash -PublishedVersions $versions.Versions -LatestContentHash $latestHash
    Write-Host $plan.Reason
    if ($plan.Action -eq 'fail') {
        return 1
    }

    if ($plan.Action -eq 'skip') {
        return 0
    }

    Push-Location $Directory
    try {
        # --allow-same-version: the first publish of a line is major.minor.0, which is already
        # the version in package.json. npm 10 exits 1 with "Version not changed" without this flag.
        $versionResult = Invoke-NpmCapture -Command $Command -- version $plan.Version --no-git-tag-version --allow-same-version
        if ($versionResult.ExitCode -ne 0) {
            Write-Host $versionResult.Output
            return $versionResult.ExitCode
        }

        Set-ContractsContentHash -PackageJson (Join-Path $Directory 'package.json') -ContentHash $hash
        $publishResult = Invoke-NpmCapture -Command $Command -- publish
        if ($publishResult.ExitCode -ne 0) {
            Write-Host $publishResult.Output
            return $publishResult.ExitCode
        }
    }
    finally {
        Pop-Location
    }

    Write-Host "Published $($plan.Version)."
    return 0
}

if ($MyInvocation.InvocationName -ne '.') {
    if (-not $ContractsDirectory) {
        $repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
        $ContractsDirectory = Join-Path $repoRoot 'contracts'
    }

    exit (Invoke-ContractsPackagePublish -Directory $ContractsDirectory -Command $NpmCommand)
}
