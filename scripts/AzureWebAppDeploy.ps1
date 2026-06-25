function New-LinuxFunctionAppZip {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDirectory,
        [Parameter(Mandatory = $true)][string]$DestinationZip
    )

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $azureFunctionsPath = Join-Path $SourceDirectory '.azurefunctions'
    if (-not (Test-Path -LiteralPath $azureFunctionsPath)) {
        throw "Publish output is missing required '.azurefunctions' folder: $SourceDirectory"
    }

    if (Test-Path $DestinationZip) {
        Remove-Item $DestinationZip -Force
    }

    $root = (Resolve-Path -LiteralPath $SourceDirectory).ProviderPath.TrimEnd('\')
    $archive = [System.IO.Compression.ZipFile]::Open($DestinationZip, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($dir in [System.IO.Directory]::EnumerateDirectories($root, '*', [System.IO.SearchOption]::AllDirectories)) {
            $relative = $dir.Substring($root.Length).TrimStart('\').Replace('\', '/')
            if ([string]::IsNullOrEmpty($relative)) {
                continue
            }

            $entryName = "$relative/"
            if (-not ($archive.Entries | Where-Object { $_.FullName -eq $entryName })) {
                $archive.CreateEntry($entryName) | Out-Null
            }
        }

        foreach ($file in [System.IO.Directory]::EnumerateFiles($root, '*', [System.IO.SearchOption]::AllDirectories)) {
            $relative = $file.Substring($root.Length).TrimStart('\').Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive,
                $file,
                $relative,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }

    $reader = [System.IO.Compression.ZipFile]::OpenRead($DestinationZip)
    try {
        $azureFunctionsEntryCount = @($reader.Entries | Where-Object { $_.FullName.StartsWith('.azurefunctions/') }).Count
        if ($azureFunctionsEntryCount -eq 0) {
            throw "Package validation failed: '.azurefunctions/' not found at zip root in $DestinationZip"
        }

        $backslashEntryCount = @($reader.Entries | Where-Object { $_.FullName -match '\\' }).Count
        if ($backslashEntryCount -gt 0) {
            throw "Package validation failed: zip contains backslash path separators (Linux incompatible): $backslashEntryCount entries"
        }

        Write-Host "Package validated: $azureFunctionsEntryCount .azurefunctions entries, forward-slash paths only."
    }
    finally {
        $reader.Dispose()
    }
}
