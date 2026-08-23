[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'AzureWebAppDeploy.ps1')

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("linux-function-zip-" + [guid]::NewGuid().ToString('N'))
$azureFunctions = Join-Path $tempRoot '.azurefunctions'
$zipPath = Join-Path ([System.IO.Path]::GetTempPath()) ("linux-function-zip-" + [guid]::NewGuid().ToString('N') + '.zip')

$windowsRelative = ConvertTo-LinuxZipEntryName -RootPath 'C:\publish\out' -FullPath 'C:\publish\out\.azurefunctions\marker.txt'
if ($windowsRelative -ne '.azurefunctions/marker.txt') {
    throw "Windows relative path was '$windowsRelative', expected '.azurefunctions/marker.txt'."
}

$linuxRelative = ConvertTo-LinuxZipEntryName -RootPath '/tmp/publish/out' -FullPath '/tmp/publish/out/.azurefunctions/marker.txt'
if ($linuxRelative -ne '.azurefunctions/marker.txt') {
    throw "Linux relative path was '$linuxRelative', expected '.azurefunctions/marker.txt'."
}

$linuxTrailing = ConvertTo-LinuxZipEntryName -RootPath '/tmp/publish/out/' -FullPath '/tmp/publish/out/.azurefunctions/'
if ($linuxTrailing -ne '.azurefunctions') {
    throw "Linux trailing-slash relative path was '$linuxTrailing', expected '.azurefunctions'."
}

try {
    New-Item -ItemType Directory -Path $azureFunctions | Out-Null
    Set-Content -LiteralPath (Join-Path $azureFunctions 'marker.txt') -Value 'ok' -NoNewline
    Set-Content -LiteralPath (Join-Path $tempRoot 'host.json') -Value '{}' -NoNewline

    New-LinuxFunctionAppZip -SourceDirectory $tempRoot -DestinationZip $zipPath

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $reader = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $names = @($reader.Entries | ForEach-Object { $_.FullName })
        $leadingSlash = @($names | Where-Object { $_.StartsWith('/') })
        if ($leadingSlash.Count -gt 0) {
            throw "Zip entries must not start with '/': $($leadingSlash -join ', ')"
        }

        $azureEntries = @($names | Where-Object { $_.StartsWith('.azurefunctions/') })
        if ($azureEntries.Count -eq 0) {
            throw "Zip is missing '.azurefunctions/' at the archive root. Entries: $($names -join ', ')"
        }

        $backslash = @($names | Where-Object { $_ -match '\\' })
        if ($backslash.Count -gt 0) {
            throw "Zip entries must use forward slashes: $($backslash -join ', ')"
        }
    }
    finally {
        $reader.Dispose()
    }
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
}

Write-Host 'assert-linux-function-zip: OK'
