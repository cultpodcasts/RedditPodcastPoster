<#
.SYNOPSIS
Publishes the public database into a SEPARATE working folder and reports what changed. Only -Apply copies it into
this repo and commits it locally (never pushing); only a later, separate -Push sends reviewed commits to the remote.

.DESCRIPTION
1. Checks the publisher exe is the version that knows --migrations-out (podcasts, TV shows, news organisations
   and films; folder-as-baseline); an older exe is refused before anything else happens.
2. Rebuilds the working folder (default: a sibling of this repo, '<repo parent>\PublicDatabase-publish') as a
   copy of this repo's current top-level *.json files. That copy is the publisher's baseline. The repo's own
   files are never deleted or modified by this step.
3. Runs the publisher inside the working folder, with --migrations-out writing a manifest next to the working
   folder (outside it, as the publisher requires). A stale manifest is deleted first.
4. Reports files added / removed / changed against the repo, the migrations from the manifest, and, for each
   migration, whether git would see a rename (R) or a delete + add. Without -Apply this is worked out with a
   throw-away git index and object store, so the repo (files, index, objects) is not touched at all.
5. Only with -Apply: first checks the repo (nothing staged, no modified or untracked top-level *.json, a named
   branch) and records HEAD, then copies the working folder's *.json into the repo (removing the ones the
   publisher removed), stages the top-level *.json (git add -A restricted to them), re-checks
   'git diff --cached -M --name-status' against the manifest and commits LOCALLY. It never pushes. It prints how to
   review the commit and how to undo it (git reset --keep <HEAD before>); if the copy, stage or commit fails part
   way, it prints how to restore the repo before rethrowing.
6. Only with -Push (a separate run, after reviewing the commit; it cannot be combined with -Apply and does not run
   the publisher): checks nothing is staged and no top-level *.json is dirty, a named branch with an upstream on a
   remote, fetches, refuses if the branch is behind or has nothing to push, shows the log and diff stat of what
   will be pushed (warning about commits that touch anything other than top-level *.json or are not
   'New episodes ...' commits), then pushes with an explicit 'git push <remote> HEAD:<upstream branch>'.

A commit and a push never happen in the same run: a local commit can be undone, a push cannot.

Publisher exit codes: 0 published; 1 refused (inconsistent data or an unexplained vanished item; nothing was
changed); 2 unusable arguments or folder; 3 unexpected failure (the working folder may be partly updated; it is
rebuilt from the repo on the next run). Whatever the code, the repo is untouched unless -Apply got past it.
-Push does not run the publisher.

.PARAMETER CommitDate
The date in the commit message 'New episodes <d MMMM yyyy>' (only used with -Apply). Default: today.
.PARAMETER Apply
Copy the published result into the repo, stage, check renames and commit LOCALLY. Never pushes: review the commit
(the script prints the commands, and how to undo it), then run again with -Push. Cannot be combined with -Push.
.PARAMETER Push
Push the commits already made (by -Apply) and reviewed, after the upstream checks; does not run the publisher or
need the exe. Cannot be combined with -Apply. (-SkipPush, from version 1.1, is gone: -Apply no longer pushes.)
.PARAMETER WorkFolder
Folder to publish into. Must be outside this repo. Reused between runs only if this script created it. A relative
path is relative to PowerShell's current location. Default: 'PublicDatabase-publish' beside this repo.
.PARAMETER AcceptRemoval
File key or id of a removal you have checked and accept (repeatable); passed to --accept-removal.
.PARAMETER PublisherExe
Override the publisher path (for testing). The default is the CultPodcasts.DatabasePublisher exe in the
artifacts\tools folder of the RedditPodcastPoster repo (see -SourceRepo).
.PARAMETER SourceRepo
The RedditPodcastPoster clone. It is used to find the default publisher exe (artifacts\tools), to look up the
commit the exe was built from (to check that it is new enough), and to check that this copy of the script is the
same as the one at that clone's HEAD. Default, a per-machine convention: with this repo at <repos>\PublicDatabase,
the clone at <repos>\cultpodcasts\RedditPodcastPoster (a 'cultpodcasts' folder beside this repo that holds the
clone). Pass -SourceRepo when the clone is elsewhere. A relative path is relative to PowerShell's location.

.NOTES
The source of truth for this script is Console-Apps/CultPodcasts.DatabasePublisher/PublicDatabaseScripts/update.ps1
in the RedditPodcastPoster repo. It runs from a copy in the root of the PublicDatabase repo (it works on the
folder it is in, and refuses to run from anywhere else), so copy it there again after every change. On every run
it compares itself (line endings ignored) with the script at HEAD of -SourceRepo and warns when they differ.
Run with -PublisherExe and -SourceRepo when the clone or the exe is not in the default place.
Written for and tested with PowerShell (5.1 and 7) on Windows; pwsh on other platforms is untested.
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [datetime]$CommitDate = (Get-Date),
    [switch]$Apply,
    [switch]$Push,
    [string]$WorkFolder,
    [string[]]$AcceptRemoval = @(),
    [string]$PublisherExe,
    [string]$SourceRepo
)

# Values may arrive as one comma-separated string (pwsh -File .\update.ps1 -AcceptRemoval a,b,c); file keys and ids
# never contain commas, so split, trim and drop empties once here.
$AcceptRemoval = @($AcceptRemoval | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })

# A human-readable label, printed at the start. Bump it when the script changes. Staleness is not judged by it: the
# script compares its own content with the copy at HEAD of -SourceRepo (see Test-ScriptIsCurrent).
$ScriptVersion = '1.2.0'

# The script's path inside the RedditPodcastPoster repo, as git names it.
$SourceScriptGitPath = 'Console-Apps/CultPodcasts.DatabasePublisher/PublicDatabaseScripts/update.ps1'

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-ExternalCommand {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter()]
        [string[]]$Arguments = @(),

        [Parameter()]
        [switch]$CaptureOutput,

        [Parameter(Mandatory)]
        [string]$Description
    )

    if ($CaptureOutput) {
        $output = & $FilePath @Arguments
    }
    else {
        & $FilePath @Arguments
        $output = $null
    }

    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }

    return $output
}

# Paths are compared ignoring case on Windows (the file system does) and exactly elsewhere.
$PathComparison = if ([System.IO.Path]::DirectorySeparatorChar -eq '\') { [System.StringComparison]::OrdinalIgnoreCase } else { [System.StringComparison]::Ordinal }

# A path from the user, made absolute against PowerShell's current location (not the .NET process directory),
# normalised, and without a trailing separator. The path does not have to exist.
function Resolve-UserPath {
    param([Parameter(Mandatory)][string]$Path)

    $resolved = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    return [System.IO.Path]::GetFullPath($resolved).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
}

# True when $Path is $Root or inside it.
function Test-PathIsWithin {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Root
    )

    return $Path.Equals($Root, $PathComparison) -or
        $Path.StartsWith($Root + [System.IO.Path]::DirectorySeparatorChar, $PathComparison)
}

# A hashtable whose keys are compared exactly, so file keys that differ only in case stay distinct (the publisher
# treats them as a delete plus a create).
function New-OrdinalTable {
    return New-Object System.Collections.Hashtable ([System.StringComparer]::Ordinal)
}

# The raw bytes git prints for a command (no PowerShell line splitting or re-encoding), or $null if git fails.
function Get-GitOutputBytes {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = 'git'
    $startInfo.Arguments = (($Arguments | ForEach-Object { '"' + ($_ -replace '"', '\"') + '"' }) -join ' ')
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    $process = [System.Diagnostics.Process]::Start($startInfo)
    try {
        $errorTask = $process.StandardError.ReadToEndAsync()
        $buffer = New-Object System.IO.MemoryStream
        $process.StandardOutput.BaseStream.CopyTo($buffer)
        $process.WaitForExit()
        [void]$errorTask.Result
        if ($process.ExitCode -ne 0) {
            return $null
        }

        return , $buffer.ToArray()
    }
    finally {
        $process.Dispose()
    }
}

# SHA-256 of a script's text with line endings normalised (CRLF -> LF) and any UTF-8 BOM dropped: the repo blob is
# LF while a Windows checkout and the PublicDatabase copy are CRLF (.gitattributes text=auto).
function Get-NormalisedTextHash {
    param([Parameter(Mandatory)][AllowEmptyCollection()][byte[]]$Bytes)

    $text = (New-Object System.Text.UTF8Encoding $false).GetString($Bytes)
    if ($text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) {
        $text = $text.Substring(1)
    }

    $text = $text.Replace("`r`n", "`n")
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [System.BitConverter]::ToString($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($text))).Replace('-', '')
    }
    finally {
        $sha.Dispose()
    }
}

# The top-level *.json files of a folder, the only files the publisher reads or writes.
function Get-JsonFiles {
    param([Parameter(Mandatory)][string]$Folder)

    return @(Get-ChildItem -LiteralPath $Folder -Filter '*.json' -File |
        Where-Object { $_.Extension -eq '.json' })
}

# file name -> SHA-256, for the top-level *.json files of a folder.
function Get-JsonHashes {
    param([Parameter(Mandatory)][string]$Folder)

    $hashes = New-OrdinalTable
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        foreach ($file in (Get-JsonFiles -Folder $Folder)) {
            $hashes[$file.Name] = [System.BitConverter]::ToString($sha.ComputeHash([System.IO.File]::ReadAllBytes($file.FullName)))
        }
    }
    finally {
        $sha.Dispose()
    }

    return $hashes
}

# Refuses a publisher that predates --migrations-out. The exe is a compressed single file, so its text cannot be
# searched; instead its embedded git hash is looked up in the RedditPodcastPoster repo and the PublishOptions.cs
# of that commit must mention --migrations-out.
function Assert-PublisherSupportsMigrations {
    param(
        [Parameter(Mandatory)][string]$Publisher,
        [Parameter(Mandatory)][string]$SourceRepo
    )

    $ErrorActionPreference = 'Continue'
    $versionText = (& $Publisher --version 2>&1 | Out-String)
    $versionExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'

    if ($versionExit -ne 0 -or $versionText -notmatch 'Git Hash:\s*([0-9a-fA-F]{40})') {
        throw "Could not read the version of '$Publisher' (--version exited $versionExit). Rebuild and republish the DatabasePublisher from RedditPodcastPoster main (scripts\publish-console-apps.ps1 -App CultPodcasts.DatabasePublisher)."
    }

    $hash = $Matches[1]
    if ($versionText -match 'Dirty:\s*Yes') {
        Write-Warning "The publisher was built from a dirty tree ($hash)."
    }

    if (-not (Test-Path -LiteralPath (Join-Path $SourceRepo '.git'))) {
        throw "Cannot confirm the publisher is new enough: the source repo '$SourceRepo' was not found. Pass the RedditPodcastPoster clone with -SourceRepo (by default it is expected in a 'cultpodcasts' folder beside this repo: <repos>\cultpodcasts\RedditPodcastPoster for <repos>\PublicDatabase)."
    }

    $ErrorActionPreference = 'Continue'
    $optionsSource = (& git -C $SourceRepo show "${hash}:Console-Apps/CultPodcasts.DatabasePublisher/PublishOptions.cs" 2>$null | Out-String)
    $showExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'

    if ($showExit -ne 0) {
        throw "Cannot confirm the publisher is new enough: its commit $hash has no PublishOptions.cs in '$SourceRepo' (an older publisher, or a commit this clone does not have). Pull main in that clone, then republish the DatabasePublisher."
    }

    if ($optionsSource -notmatch '--migrations-out') {
        throw "The publisher at '$Publisher' is an old build ($hash) that does not support --migrations-out or the folder-as-baseline checks. Pull main in the RedditPodcastPoster clone and republish the DatabasePublisher. Nothing was changed."
    }

    Write-Host "Publisher version OK: $hash"
    return [string]$hash
}

# Warns (never fails) when this copy of the script differs from the script at HEAD of the RedditPodcastPoster clone
# (the source of truth), ignoring line endings, so a stale copy in the PublicDatabase repo is noticed. HEAD is the
# clone's checked-out commit: pull the clone first to compare with the latest main.
function Test-ScriptIsCurrent {
    param(
        [Parameter(Mandatory)][string]$ScriptPath,
        [Parameter(Mandatory)][string]$SourceRepo,
        [Parameter(Mandatory)][string]$ThisVersion
    )

    if (-not (Test-Path -LiteralPath (Join-Path $SourceRepo '.git'))) {
        Write-Warning "Could not check that this copy of update.ps1 is current: the source repo '$SourceRepo' was not found."
        return
    }

    $theirs = Get-GitOutputBytes -Arguments @('-C', $SourceRepo, 'show', "HEAD:$SourceScriptGitPath")
    if ($null -eq $theirs) {
        Write-Warning "Could not check that this copy of update.ps1 is current: HEAD of '$SourceRepo' has no $SourceScriptGitPath (an old checkout or another branch)."
        return
    }

    $mine = Get-NormalisedTextHash -Bytes ([System.IO.File]::ReadAllBytes($ScriptPath))
    if ($mine -eq (Get-NormalisedTextHash -Bytes $theirs)) {
        Write-Host "This copy of update.ps1 (version $ThisVersion) is identical to the one at HEAD of '$SourceRepo'."
        return
    }

    $theirText = (New-Object System.Text.UTF8Encoding $false).GetString($theirs)
    $theirVersion = if ($theirText -match '(?m)^\$ScriptVersion\s*=\s*''([^'']+)''') { $Matches[1] } else { 'no version line' }
    Write-Warning ("This copy of update.ps1 (version $ThisVersion) differs from the one at HEAD of '$SourceRepo' (version $theirVersion). " +
        "If the clone is up to date, this copy is stale: copy $SourceScriptGitPath from the clone over '$ScriptPath'. " +
        "If this copy was edited here, move the edit into the clone instead (the clone is the source of truth). If the clone is behind, pull it.")
}

# 'git diff --cached -M --name-status' as it would be if the working folder's *.json were staged in the repo,
# worked out with a temporary index and object directory (the repo's real index, objects and files are not touched).
function Get-PreviewNameStatus {
    param(
        [Parameter(Mandatory)][string]$RepoRoot,
        [Parameter(Mandatory)][string]$Folder
    )

    $gitDir = ((Invoke-ExternalCommand -FilePath 'git' -Arguments @('-C', $RepoRoot, 'rev-parse', '--absolute-git-dir') -CaptureOutput -Description 'Finding the git directory') | Select-Object -First 1)
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('update-ps1-preview-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path (Join-Path $tempRoot 'objects') | Out-Null

    $names = @('GIT_DIR', 'GIT_WORK_TREE', 'GIT_INDEX_FILE', 'GIT_OBJECT_DIRECTORY', 'GIT_ALTERNATE_OBJECT_DIRECTORIES')
    $saved = @{}
    foreach ($name in $names) {
        $saved[$name] = [System.Environment]::GetEnvironmentVariable($name)
    }

    Push-Location $Folder
    try {
        $env:GIT_DIR = $gitDir
        $env:GIT_WORK_TREE = $Folder
        $env:GIT_INDEX_FILE = Join-Path $tempRoot 'index'
        $env:GIT_OBJECT_DIRECTORY = Join-Path $tempRoot 'objects'
        $env:GIT_ALTERNATE_OBJECT_DIRECTORIES = Join-Path $gitDir 'objects'

        $quiet = @('-c', 'core.fsmonitor=false', '-c', 'core.untrackedCache=false', '-c', 'core.quotepath=false')
        Invoke-ExternalCommand -FilePath 'git' -Arguments ($quiet + @('read-tree', 'HEAD')) -Description 'Preview: reading HEAD into a temporary index' | Out-Null
        Invoke-ExternalCommand -FilePath 'git' -Arguments ($quiet + @('add', '-A', '--', ':(glob)*.json')) -Description 'Preview: staging into a temporary index' | Out-Null
        $lines = Invoke-ExternalCommand -FilePath 'git' -Arguments ($quiet + @('diff', '--cached', '-M', '--name-status', '--', ':(glob)*.json')) -CaptureOutput -Description 'Preview: diffing the temporary index'
        return @($lines | Where-Object { $_ })
    }
    finally {
        Pop-Location
        foreach ($name in $names) {
            # Restore, or remove if it was unset (setting $null can leave an empty value that breaks git in the caller's session).
            if ($null -eq $saved[$name]) {
                Remove-Item -LiteralPath ('Env:' + $name) -ErrorAction SilentlyContinue
            }
            else {
                [System.Environment]::SetEnvironmentVariable($name, $saved[$name])
            }
        }

        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# For each manifest migration: did git pair the old and new file as a rename, or show a delete + add?
function Get-MigrationGitRows {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Migrations,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$NameStatus
    )

    $renamed = New-OrdinalTable
    $deleted = New-OrdinalTable
    $added = New-OrdinalTable
    $modified = New-OrdinalTable
    foreach ($line in $NameStatus) {
        $parts = $line -split "`t"
        $code = $parts[0]
        if ($code -match '^R(\d+)$' -and $parts.Count -ge 3) {
            $renamed[$parts[1]] = [pscustomobject]@{ New = $parts[2]; Score = [int]$Matches[1] }
        }
        elseif ($code -eq 'D' -and $parts.Count -ge 2) { $deleted[$parts[1]] = $true }
        elseif ($code -eq 'A' -and $parts.Count -ge 2) { $added[$parts[1]] = $true }
        elseif ($code -eq 'M' -and $parts.Count -ge 2) { $modified[$parts[1]] = $true }
    }

    $rows = @()
    foreach ($migration in $Migrations) {
        $old = [string]$migration.oldFile
        $new = [string]$migration.newFile
        if ($renamed.ContainsKey($old) -and $renamed[$old].New -ceq $new) {
            $result = 'rename'
            $detail = "R$($renamed[$old].Score)"
        }
        elseif ($renamed.ContainsKey($old)) {
            $result = 'other'
            $detail = "git paired the old file with '$($renamed[$old].New)' instead (it has several successors)"
        }
        elseif ($deleted.ContainsKey($old) -and $added.ContainsKey($new)) {
            $result = 'delete+add'
            $detail = 'git did not pair them (content too different)'
        }
        elseif ($deleted.ContainsKey($old) -and $modified.ContainsKey($new)) {
            $result = 'delete+modify'
            $detail = 'the new file already existed, so no rename is possible'
        }
        else {
            $result = 'other'
            $detail = 'not as expected in the diff (old file kept, or no change)'
        }

        $rows += [pscustomobject]@{
            Result = $result
            Detail = $detail
            Reason = [string]$migration.reason
            Kind   = [string]$migration.kind
            Id     = [string]$migration.id
            Old    = $old
            New    = $new
        }
    }

    return $rows
}

function Write-MigrationGitReport {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Rows,
        [Parameter(Mandatory)][string]$Label
    )

    Write-Host ''
    Write-Host "Migrations in the manifest and what git sees ($Label): $($Rows.Count)"
    foreach ($row in $Rows) {
        Write-Host ("  [{0} {1}] {2}: {3} -> {4} ({5}, id {6})" -f $row.Result, $row.Detail, $row.Reason, $row.Old, $row.New, $row.Kind, $row.Id)
    }

    $renames = @($Rows | Where-Object { $_.Result -eq 'rename' }).Count
    $deleteAdd = @($Rows | Where-Object { $_.Result -eq 'delete+add' }).Count
    $other = $Rows.Count - $renames - $deleteAdd
    Write-Host "  Summary: $renames shown as rename, $deleteAdd as delete + add, $other other."
}

function Write-NameList {
    param(
        [Parameter(Mandatory)][string]$Title,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Names,
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.List[string]]$Report,
        [int]$Show = 25
    )

    Write-Host "  ${Title}: $($Names.Count)"
    $Report.Add("${Title}: $($Names.Count)")
    foreach ($name in $Names) {
        $Report.Add("  $name")
    }

    foreach ($name in ($Names | Select-Object -First $Show)) {
        Write-Host "    $name"
    }

    if ($Names.Count -gt $Show) {
        Write-Host "    ... $($Names.Count - $Show) more (full list in the report file)"
    }
}

# After a refused (exit 1) run: show, unmissably, why. The publisher's own printed plan has already been shown above;
# its text is parsed here only for two blocks: the removals it could not explain ('UNEXPLAINED ...') and the
# --accept-removal values that matched none of them ('--accept-removal values that matched no unexplained removal').
# A block that cannot be parsed is echoed raw, so a change of wording degrades to raw text.
function Write-RefusalSummary {
    param([Parameter(Mandatory)][AllowEmptyCollection()][string[]]$PublisherLines)

    $staleHeading = 'matched no unexplained removal'
    $block = New-Object 'System.Collections.Generic.List[string]'
    $staleValues = New-Object 'System.Collections.Generic.List[string]'
    $staleHeadingLine = $null
    $section = 'none'
    foreach ($line in $PublisherLines) {
        if ($line -match '^\s*Check each file') { $section = 'none'; continue }
        if ($line -match [regex]::Escape($staleHeading)) { $section = 'stale'; $staleHeadingLine = $line.Trim(); continue }
        if ($line -match '^\s*UNEXPLAINED') { $section = 'unexplained' }

        switch ($section) {
            'unexplained' { $block.Add($line) }
            'stale' {
                # The values are listed one per indented line; the first unindented line ends the list.
                if ($line -match '^\s+(\S.*)$') { $staleValues.Add($Matches[1].Trim()) } else { $section = 'none' }
            }
        }
    }

    $bar = '!' * 78
    Write-Host ''
    Write-Host $bar -ForegroundColor Red
    Write-Host 'PUBLISH REFUSED - NOTHING WAS PUBLISHED. The repo and the working folder were not changed.' -ForegroundColor Red
    Write-Host $bar -ForegroundColor Red

    if ($staleHeadingLine) {
        Write-Host ''
        Write-Host "STALE -AcceptRemoval VALUES ($($staleValues.Count)): these match no unexplained removal in this run, so the publisher refused rather than ignore them:" -ForegroundColor Yellow
        foreach ($value in $staleValues) { Write-Host "  * $value" -ForegroundColor Yellow }
        if ($staleValues.Count -eq 0) { Write-Host "  (could not read the list; the publisher said: $staleHeadingLine)" -ForegroundColor Yellow }
        Write-Host 'Drop or correct them (a removal already published, or a mistyped file key or id) and run again.' -ForegroundColor Cyan
    }

    if ($block.Count -eq 0) {
        if (-not $staleHeadingLine) {
            Write-Host 'The publisher did not print an UNEXPLAINED block, so the refusal is for another reason (inconsistent data).' -ForegroundColor Red
            Write-Host 'Read the publisher output above.' -ForegroundColor Red
        }

        return
    }

    $joined = ($block -join '')
    $idPattern = '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}'
    $items = @([regex]::Matches($joined, "'([^']+)'\s*\(id\s*($idPattern)\)\s*(.*?)(?=(?:'[^']+'\s*\(id\s)|Check each file|--accept-removal values|$)") |
        ForEach-Object { [pscustomobject]@{ File = $_.Groups[1].Value; Id = $_.Groups[2].Value; Why = $_.Groups[3].Value.Trim() } })

    Write-Host ''
    Write-Host "UNEXPLAINED REMOVALS ($($items.Count)): these published files would DISAPPEAR and the publisher cannot say why:" -ForegroundColor Yellow
    if ($items.Count -eq 0) {
        Write-Host 'Could not parse the list; the publisher text is:' -ForegroundColor Yellow
        foreach ($line in $block) { Write-Host "  $line" -ForegroundColor Yellow }
        return
    }

    foreach ($item in $items) {
        Write-Host ("  * {0}  (id {1})" -f $item.File, $item.Id) -ForegroundColor Yellow
        if ($item.Why) { Write-Host ("      {0}" -f $item.Why) -ForegroundColor Yellow }
    }

    Write-Host ''
    Write-Host 'Check each one (is the podcast really gone, or is its data missing?). To accept the ones you have checked, run again with:' -ForegroundColor Cyan
    foreach ($item in $items) {
        Write-Host ("  .\update.ps1 -AcceptRemoval {0}      # {1}" -f $item.Id, $item.File) -ForegroundColor Cyan
    }
    Write-Host ('  or all at once: .\update.ps1 -AcceptRemoval ' + (($items | ForEach-Object { $_.Id }) -join ',')) -ForegroundColor Cyan
}

# Runs git and shows its output on the console (Invoke-ExternalCommand's uncaptured output would be swallowed by the
# caller's pipeline).
function Write-GitOutput {
    param(
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$Description
    )

    foreach ($line in @(Invoke-ExternalCommand -FilePath 'git' -Arguments $Arguments -CaptureOutput -Description $Description)) {
        Write-Host "  $line"
    }
}

# The checked-out branch, or a refusal (naming the action) when HEAD is detached.
function Get-CurrentBranch {
    param([Parameter(Mandatory)][string]$Action)

    $ErrorActionPreference = 'Continue'
    $branch = (& git symbolic-ref --quiet --short HEAD 2>$null | Out-String).Trim()
    $branchExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($branchExit -ne 0 -or -not $branch) {
        throw "$Action needs a checked-out branch, but HEAD is detached. Check out the branch (normally main) first. Nothing was changed."
    }

    return $branch
}

# Refuses when anything is staged or a top-level *.json is modified or untracked, so a commit (or a push) holds only
# what was published and reviewed.
function Assert-NothingStagedOrDirtyJson {
    param(
        [Parameter(Mandatory)][string]$Action,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$DirtyJson
    )

    $staged = @(Invoke-ExternalCommand -FilePath 'git' -Arguments @('diff', '--cached', '--name-only') -CaptureOutput -Description 'Checking for staged changes' | Where-Object { $_ })
    if ($staged.Count -gt 0 -or $DirtyJson.Count -gt 0) {
        throw "$Action needs a repo with nothing staged and no modified or untracked top-level *.json files ($($staged.Count) staged, $($DirtyJson.Count) dirty *.json). Commit, stash or restore them first. Nothing was changed."
    }
}

# Where -Push will push, checked before anything is pushed: the branch's upstream on a real remote (not '.'), fetched,
# with the branch not behind it and at least one commit ahead. Returns the remote, the upstream's ref on that remote
# (branch.<b>.merge) and the counts.
function Get-PushTarget {
    param([Parameter(Mandatory)][string]$Branch)

    $ErrorActionPreference = 'Continue'
    $upstream = (& git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>$null | Out-String).Trim()
    $upstreamExit = $LASTEXITCODE
    $remote = (& git config --get "branch.$Branch.remote" 2>$null | Out-String).Trim()
    $mergeRef = (& git config --get "branch.$Branch.merge" 2>$null | Out-String).Trim()
    $ErrorActionPreference = 'Stop'
    if ($upstreamExit -ne 0 -or -not $upstream -or -not $remote -or -not $mergeRef -or $remote -eq '.') {
        throw "-Push needs branch '$Branch' to have an upstream on a remote, and it has none. Set one (git branch --set-upstream-to=origin/$Branch) and run again. Nothing was pushed."
    }

    Write-Host "Fetching $remote to check '$Branch' against $upstream..."
    Invoke-ExternalCommand -FilePath 'git' -Arguments @('fetch', '--quiet', $remote) -Description "Fetching $remote" | Out-Null
    $behind = [int](Invoke-ExternalCommand -FilePath 'git' -Arguments @('rev-list', '--count', 'HEAD..@{u}') -CaptureOutput -Description 'Counting commits behind the upstream' | Select-Object -First 1)
    $ahead = [int](Invoke-ExternalCommand -FilePath 'git' -Arguments @('rev-list', '--count', '@{u}..HEAD') -CaptureOutput -Description 'Counting commits ahead of the upstream' | Select-Object -First 1)

    if ($behind -gt 0) {
        $base = ((Invoke-ExternalCommand -FilePath 'git' -Arguments @('merge-base', 'HEAD', '@{u}') -CaptureOutput -Description 'Finding the merge base') | Select-Object -First 1)
        throw ("Branch '$Branch' is $behind commit(s) behind $upstream (and $ahead ahead), so the push would be rejected and the local publish is based on a stale baseline. Nothing was pushed. " +
            "Undo the local publish commit(s) with 'git reset --keep $base', pull ('git pull --ff-only'), then run -Apply again and review before -Push.")
    }

    if ($ahead -eq 0) {
        throw "Branch '$Branch' has no commits that are not already on $upstream, so there is nothing to push. Run -Apply first (and review its commit). Nothing was pushed."
    }

    return [pscustomobject]@{ Remote = [string]$remote; Ref = [string]$mergeRef; Branch = [string]$Branch; Upstream = [string]$upstream; Ahead = $ahead }
}

# Shows what -Push would send (log and diff stat of @{u}..HEAD) and returns warnings for commits that are not plain
# publish commits: a subject other than 'New episodes ...', or a change to anything but top-level *.json.
function Write-PushPreview {
    param([Parameter(Mandatory)][object]$Target)

    Write-Host ''
    Write-Host "Commits that will be pushed to $($Target.Remote) ($($Target.Upstream)): $($Target.Ahead)"
    Write-GitOutput -Arguments @('--no-pager', 'log', '--oneline', '--no-decorate', '@{u}..HEAD') -Description 'Listing the commits to push'
    Write-Host ''
    Write-Host 'What they change (git diff --stat -M @{u}..HEAD):'
    Write-GitOutput -Arguments @('-c', 'core.quotepath=false', '--no-pager', 'diff', '--stat=200', '-M', '@{u}..HEAD') -Description 'Showing the diff stat to push'

    $warnings = New-Object 'System.Collections.Generic.List[string]'
    $commits = @(Invoke-ExternalCommand -FilePath 'git' -Arguments @('rev-list', '--reverse', '@{u}..HEAD') -CaptureOutput -Description 'Listing the commits to push' | Where-Object { $_ })
    foreach ($commit in $commits) {
        $subject = ((Invoke-ExternalCommand -FilePath 'git' -Arguments @('log', '-1', '--format=%s', $commit) -CaptureOutput -Description 'Reading a commit subject') | Select-Object -First 1)
        $short = $commit.Substring(0, 8)
        $parents = @(((Invoke-ExternalCommand -FilePath 'git' -Arguments @('log', '-1', '--format=%P', $commit) -CaptureOutput -Description 'Reading commit parents') | Select-Object -First 1) -split ' ' | Where-Object { $_ })
        if ($parents.Count -gt 1) {
            $warnings.Add("$short '$subject' is a merge commit, not a publish commit.")
        }

        if ([string]$subject -notmatch '^New episodes ') {
            $warnings.Add("$short '$subject' is not a 'New episodes ...' publish commit.")
        }

        $files = @(Invoke-ExternalCommand -FilePath 'git' -Arguments @('-c', 'core.quotepath=false', 'diff-tree', '--no-commit-id', '--name-only', '-r', '--root', $commit) -CaptureOutput -Description 'Listing a commit''s files' | Where-Object { $_ })
        $other = @($files | Where-Object { $_ -notmatch '^[^/]+\.json$' })
        if ($other.Count -gt 0) {
            $shown = ($other | Select-Object -First 5) -join ', '
            $more = if ($other.Count -gt 5) { " and $($other.Count - 5) more" } else { '' }
            $warnings.Add("$short '$subject' changes $($other.Count) file(s) that are not top-level *.json: $shown$more.")
        }
    }

    return $warnings.ToArray()
}

# After an -Apply commit: how to review it, how to undo it, and what comes next.
function Write-ApplyCommitGuide {
    param([Parameter(Mandatory)][string]$PreSha)

    $short = $PreSha.Substring(0, 8)
    Write-Host ''
    Write-Host 'The commit (git show --stat HEAD):'
    Write-GitOutput -Arguments @('-c', 'core.quotepath=false', '--no-pager', 'show', '--stat=200', '-M', '--format=fuller', 'HEAD') -Description 'Showing the commit'
    Write-Host ''
    Write-Host "Against the HEAD before this run ($short) (git diff -M --shortstat $short HEAD):"
    Write-GitOutput -Arguments @('--no-pager', 'diff', '-M', '--shortstat', $PreSha, 'HEAD') -Description 'Diffing against the HEAD before this run'
    Write-Host ''
    Write-Host 'COMMITTED LOCALLY ONLY - NOTHING WAS PUSHED.' -ForegroundColor Green
    Write-Host 'Review it:' -ForegroundColor Cyan
    Write-Host '  git show --stat HEAD' -ForegroundColor Cyan
    Write-Host "  git diff -M --stat $PreSha HEAD" -ForegroundColor Cyan
    Write-Host "Undo it (puts the repo back exactly as it was before this run; --keep refuses rather than discard other local edits):" -ForegroundColor Cyan
    Write-Host "  git reset --keep $PreSha" -ForegroundColor Cyan
    Write-Host 'When you are happy with it, push it:' -ForegroundColor Cyan
    Write-Host '  .\update.ps1 -Push' -ForegroundColor Cyan
}

# After a failure part way through -Apply's copy / stage / commit: how to put the repo back.
function Write-ApplyRecovery {
    param([Parameter(Mandatory)][string]$PreSha)

    $ErrorActionPreference = 'Continue'
    $headNow = (& git rev-parse HEAD 2>$null | Out-String).Trim()
    $ErrorActionPreference = 'Stop'

    $bar = '!' * 78
    Write-Host ''
    Write-Host $bar -ForegroundColor Red
    Write-Host '-Apply FAILED PART WAY. Nothing was pushed, but the repo may hold part of this publish.' -ForegroundColor Red
    Write-Host $bar -ForegroundColor Red
    if ($headNow -and $headNow -ne $PreSha) {
        Write-Host "A commit was made ($($headNow.Substring(0, 8))). Undo it with:" -ForegroundColor Cyan
        Write-Host "  git reset --keep $PreSha" -ForegroundColor Cyan
    }
    else {
        Write-Host 'No commit was made. Put the top-level *.json back as they were (restores modified and deleted files, then removes added ones):' -ForegroundColor Cyan
        Write-Host "  git restore --source=HEAD --staged --worktree -- ':(glob)*.json'" -ForegroundColor Cyan
        Write-Host "  git clean -f -- ':(glob)*.json'" -ForegroundColor Cyan
    }

    Write-Host "Then check with: git status (HEAD should be $PreSha)" -ForegroundColor Cyan
}

Write-Host "update.ps1 version $ScriptVersion"

if ($Apply -and $Push) {
    throw "-Apply and -Push cannot be used together: -Apply commits locally, you review the commit, then a separate run of -Push pushes it (a push cannot be undone). Nothing was changed."
}

$scriptPath = $PSCommandPath
if (-not $PSScriptRoot -or -not $scriptPath) {
    throw "update.ps1 must be run as a script file (.\update.ps1 from the root of the PublicDatabase repo), not dot-sourced or pasted."
}

$repoRoot = Resolve-UserPath -Path $PSScriptRoot
if (-not $SourceRepo) {
    $SourceRepo = Join-Path (Join-Path (Split-Path -Parent $repoRoot) 'cultpodcasts') 'RedditPodcastPoster'
}

$sourceRepoPath = Resolve-UserPath -Path $SourceRepo

# The script works on the folder it is in, so that folder must be the root of the PublicDatabase checkout: not the
# source copy in RedditPodcastPoster, and not a sub-folder (git would then work on the enclosing repo while the
# sub-folder was treated as the baseline).
$wrongPlace = "copy this script into the root of the PublicDatabase repo and run it from there (see Console-Apps/README.md in RedditPodcastPoster)."
$ErrorActionPreference = 'Continue'
$topLevel = (& git -C $repoRoot rev-parse --show-toplevel 2>$null | Out-String).Trim()
$topLevelExit = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($topLevelExit -ne 0 -or -not $topLevel) {
    throw "'$repoRoot' is not inside a git repo: $wrongPlace"
}

$topLevelPath = Resolve-UserPath -Path ([string]$topLevel)
if (-not $topLevelPath.Equals($repoRoot, $PathComparison)) {
    throw "This script is in '$repoRoot', which is not the root of its git repo ('$topLevelPath'): $wrongPlace"
}

if (Test-PathIsWithin -Path $repoRoot -Root $sourceRepoPath) {
    throw "This script is running from inside the source repo '$sourceRepoPath' (the source of truth is not a publish target): $wrongPlace"
}

if ($Push) {
    $ignored = @('CommitDate', 'WorkFolder', 'AcceptRemoval', 'PublisherExe') | Where-Object { $PSBoundParameters.ContainsKey($_) }
    if ($ignored) {
        Write-Warning ("-Push does not publish, so these are ignored: -" + ($ignored -join ', -'))
    }

    Push-Location $repoRoot
    try {
        Test-ScriptIsCurrent -ScriptPath $scriptPath -SourceRepo $sourceRepoPath -ThisVersion $ScriptVersion
        $dirtyJson = @(Invoke-ExternalCommand -FilePath 'git' -Arguments @('-c', 'core.quotepath=false', 'status', '--porcelain', '--', ':(glob)*.json') -CaptureOutput -Description 'Checking the JSON files are committed' | Where-Object { $_ })
        Assert-NothingStagedOrDirtyJson -Action '-Push' -DirtyJson $dirtyJson
        $branch = Get-CurrentBranch -Action '-Push'
        $target = Get-PushTarget -Branch $branch
        $warnings = @(Write-PushPreview -Target $target)
        if ($warnings.Count -gt 0) {
            Write-Host ''
            foreach ($warning in $warnings) {
                Write-Warning $warning
            }
            # A push cannot be undone, so a warning here must be answered before anything is sent. Anything but 'y'
            # (including no console to ask on) refuses.
            $answer = Read-Host "The commits above are not all plain publish commits and would be pushed too. Push anyway? [y/N]"
            if ($answer -notmatch '^(y|yes)$') {
                throw "Not pushed: the commits to push are not all plain publish commits (see the warnings above). Nothing was pushed."
            }
        }

        $headSha = ((Invoke-ExternalCommand -FilePath 'git' -Arguments @('rev-parse', 'HEAD') -CaptureOutput -Description 'Reading HEAD') | Select-Object -First 1)
        Write-Host ''
        Write-Host "Pushing '$($target.Branch)' ($($headSha.Substring(0, 8))) to $($target.Remote) as $($target.Ref)..." -ForegroundColor Yellow
        # Pushed explicitly to the upstream checked above, never to whatever a bare 'git push' picks.
        Invoke-ExternalCommand -FilePath 'git' -Arguments @('push', $target.Remote, "HEAD:$($target.Ref)") -Description 'Pushing git changes' | Out-Null
        Write-Host "Pushed $($target.Ahead) commit(s) to $($target.Remote) ($($target.Upstream))." -ForegroundColor Green
    }
    finally {
        Pop-Location
    }

    return
}

if ($PublisherExe) {
    $publisherPath = Resolve-UserPath -Path $PublisherExe
    if (-not (Test-Path -LiteralPath $publisherPath -PathType Leaf)) {
        throw "Publisher '$publisherPath' was not found."
    }
}
else {
    $publisherBasePath = Join-Path (Join-Path (Join-Path $sourceRepoPath 'artifacts') 'tools') 'CultPodcasts.DatabasePublisher'
    $publisherPath = @(
        "$publisherBasePath.exe",
        $publisherBasePath
    ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1

    if (-not $publisherPath) {
        throw "Could not find CultPodcasts.DatabasePublisher. Expected '$publisherBasePath.exe' or '$publisherBasePath' (publish it with scripts\publish-console-apps.ps1 -App CultPodcasts.DatabasePublisher in the RedditPodcastPoster clone, or pass -PublisherExe and -SourceRepo)."
    }
}

# The working folder must be a sibling-style folder outside the repo, never the repo or a parent or child of it.
if (-not $WorkFolder) {
    $WorkFolder = Join-Path (Split-Path -Parent $repoRoot) 'PublicDatabase-publish'
}

$workFolderPath = Resolve-UserPath -Path $WorkFolder
if ((Test-PathIsWithin -Path $workFolderPath -Root $repoRoot) -or (Test-PathIsWithin -Path $repoRoot -Root $workFolderPath)) {
    throw "The working folder '$workFolderPath' must be outside the repo '$repoRoot'."
}

$manifestPath = $workFolderPath + '.migrations.json'
$reportPath = $workFolderPath + '.report.txt'
$markerName = '.update-ps1-work-folder'
$commitMessage = 'New episodes {0}' -f $CommitDate.ToString('d MMMM yyyy', [System.Globalization.CultureInfo]::GetCultureInfo('en-GB'))

Push-Location $repoRoot

try {
    [void](Assert-PublisherSupportsMigrations -Publisher $publisherPath -SourceRepo $sourceRepoPath)
    Test-ScriptIsCurrent -ScriptPath $scriptPath -SourceRepo $sourceRepoPath -ThisVersion $ScriptVersion

    $repoFiles = Get-JsonFiles -Folder $repoRoot
    if ($repoFiles.Count -eq 0) {
        throw "The repo has no top-level *.json files to use as the baseline; this script does not do a first publish."
    }

    $dirtyJson = @(Invoke-ExternalCommand -FilePath 'git' -Arguments @('-c', 'core.quotepath=false', 'status', '--porcelain', '--', ':(glob)*.json') -CaptureOutput -Description 'Checking the JSON files are committed' | Where-Object { $_ })
    $preSha = $null
    if ($Apply) {
        # Everything -Apply needs is checked here, before anything is copied, staged or committed.
        Assert-NothingStagedOrDirtyJson -Action '-Apply' -DirtyJson $dirtyJson
        $branch = Get-CurrentBranch -Action '-Apply'
        # Recorded before anything is copied: the undo point printed after the commit (or after a failure).
        $preSha = [string]((Invoke-ExternalCommand -FilePath 'git' -Arguments @('rev-parse', 'HEAD') -CaptureOutput -Description 'Reading HEAD') | Select-Object -First 1)
        Write-Host "Will commit to branch '$branch' (now at $($preSha.Substring(0, 8))) LOCALLY ONLY; nothing is pushed. Review the commit, then run .\update.ps1 -Push." -ForegroundColor Yellow
    }

    elseif ($dirtyJson.Count -gt 0) {
        Write-Warning ("The repo has $($dirtyJson.Count) modified or untracked top-level *.json file(s). The Added/Removed/Changed lists compare with " +
            "these working-tree files, but the git rename view compares with HEAD, so the two can disagree. -Apply refuses until they are committed, stashed or restored.")
    }

    $repoHashes = Get-JsonHashes -Folder $repoRoot

    # Rebuild the working folder as a copy of the repo's current files: the publisher's baseline.
    if (Test-Path -LiteralPath $workFolderPath) {
        $isEmpty = -not (Get-ChildItem -LiteralPath $workFolderPath -Force | Select-Object -First 1)
        if (-not $isEmpty -and -not (Test-Path -LiteralPath (Join-Path $workFolderPath $markerName))) {
            throw "The working folder '$workFolderPath' exists, is not empty and was not created by this script, so it will not be cleared. Choose another -WorkFolder or empty it yourself."
        }

        Write-Host "Clearing the working folder $workFolderPath..."
        Remove-Item -LiteralPath $workFolderPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $workFolderPath | Out-Null
    Set-Content -LiteralPath (Join-Path $workFolderPath $markerName) -Value 'Created by update.ps1; safe for it to clear.'

    Write-Host "Copying $($repoFiles.Count) existing JSON files from the repo into the working folder (the repo is not changed)..."
    foreach ($file in $repoFiles) {
        Copy-Item -LiteralPath $file.FullName -Destination $workFolderPath
    }

    # A manifest from an earlier run must never be mistaken for this run's.
    foreach ($stale in @($manifestPath, $manifestPath + '.tmp', $reportPath)) {
        if (Test-Path -LiteralPath $stale) {
            Remove-Item -LiteralPath $stale -Force
        }
    }

    $publisherArguments = @('--migrations-out', $manifestPath)
    foreach ($removal in $AcceptRemoval) {
        $publisherArguments += @('--accept-removal', $removal)
    }

    Write-Host "Running CultPodcasts.DatabasePublisher in $workFolderPath..."
    # Output is echoed as it arrives and kept, so a refusal (exit 1) can be summarised afterwards.
    $publisherLines = New-Object 'System.Collections.Generic.List[string]'
    Push-Location $workFolderPath
    $ErrorActionPreference = 'Continue'
    try {
        & $publisherPath @publisherArguments 2>&1 | ForEach-Object {
            $text = [string]$_
            $publisherLines.Add($text)
            Write-Host $text
        }
        $publisherExit = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = 'Stop'
        Pop-Location
    }

    $refused = ($publisherExit -eq 1)
    switch ($publisherExit) {
        0 { }
        1 { }
        2 { throw "The publisher rejected its arguments or the working folder (exit 2). The repo was not changed. If this is an old publisher, republish the DatabasePublisher from RedditPodcastPoster main." }
        3 { throw "The publisher failed unexpectedly (exit 3). The working folder '$workFolderPath' may be partly updated (staged *.json.tmp files may remain); it is rebuilt from the repo on the next run. The repo was not changed." }
        default { throw "The publisher exited with unexpected code $publisherExit. The repo was not changed." }
    }

    $refusalMessage = "The publisher refused to publish (exit 1): the data cannot be published consistently, or an item would vanish unexplained. The repo was not changed. For a removal you have checked, run again with -AcceptRemoval <fileKey or id> (see above)."

    if ($refused -and -not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        # A refused run writes no manifest and changes nothing; the publisher's own plan above is all there is to show.
        Write-Host ''
        Write-Host 'Because the publisher refused, it wrote no migrations manifest and changed nothing, so there is no added/removed/changed list or git rename view for this run. The plan it printed above (counts, moved files, created files) is what it would do once the unexplained removals are resolved.' -ForegroundColor DarkYellow
        Write-RefusalSummary -PublisherLines $publisherLines.ToArray()
        throw $refusalMessage
    }

    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "The publisher succeeded but did not write the migrations manifest '$manifestPath'; it is probably an old build. The repo was not changed."
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if (-not $manifest.PSObject.Properties['version'] -or $manifest.version -ne 1 -or -not $manifest.PSObject.Properties['migrations']) {
        throw "The migrations manifest '$manifestPath' is not the expected version 1 format. The repo was not changed."
    }

    $migrations = @($manifest.migrations)

    $leftovers = @(Get-ChildItem -LiteralPath $workFolderPath -Filter '*.json.tmp' -File)
    if ($leftovers.Count -gt 0 -and -not $refused) {
        throw "The publisher succeeded but left $($leftovers.Count) staged *.json.tmp file(s) in '$workFolderPath'. The repo was not changed."
    }

    # What the publish changed, against the repo's current files.
    $workHashes = Get-JsonHashes -Folder $workFolderPath
    $addedFiles = @($workHashes.Keys | Where-Object { -not $repoHashes.ContainsKey($_) } | Sort-Object)
    $removedFiles = @($repoHashes.Keys | Where-Object { -not $workHashes.ContainsKey($_) } | Sort-Object)
    $changedFiles = @($workHashes.Keys | Where-Object { $repoHashes.ContainsKey($_) -and $repoHashes[$_] -ne $workHashes[$_] } | Sort-Object)
    $migratedOld = New-OrdinalTable
    foreach ($migration in $migrations) { $migratedOld[[string]$migration.oldFile] = $true }
    $removedUnexplained = @($removedFiles | Where-Object { -not $migratedOld.ContainsKey($_) })

    $report = New-Object 'System.Collections.Generic.List[string]'
    Write-Host ''
    Write-Host "Publish result against the repo's current files ($($repoHashes.Count) before, $($workHashes.Count) after):"
    Write-NameList -Title 'Added' -Names $addedFiles -Report $report
    Write-NameList -Title 'Removed' -Names $removedFiles -Report $report
    Write-NameList -Title 'Removed with no migration entry' -Names $removedUnexplained -Report $report
    Write-NameList -Title 'Changed' -Names $changedFiles -Report $report
    Set-Content -LiteralPath $reportPath -Value $report
    Write-Host "  Full lists: $reportPath"
    Write-Host "  Migrations manifest: $manifestPath"

    $changeCount = $addedFiles.Count + $removedFiles.Count + $changedFiles.Count

    if (-not $Apply) {
        if ($changeCount -gt 0) {
            if ($dirtyJson.Count -gt 0) {
                Write-Warning 'The rename view below compares with HEAD, not with the uncommitted *.json files in the working tree (see the warning above).'
            }

            $previewStatus = @(Get-PreviewNameStatus -RepoRoot $repoRoot -Folder $workFolderPath)
            Write-MigrationGitReport -Rows @(Get-MigrationGitRows -Migrations $migrations -NameStatus $previewStatus) -Label 'preview with a temporary index; the repo is untouched'
        }
        else {
            Write-MigrationGitReport -Rows @() -Label 'preview'
        }

        if ($refused) {
            Write-RefusalSummary -PublisherLines $publisherLines.ToArray()
            throw $refusalMessage
        }

        Write-Host ''
        Write-Host 'The repo was not changed. Review the above and the working folder, then run again with -Apply to copy the result into the repo, stage it and commit it LOCALLY (it never pushes). Review that commit, then run -Push to push it. Apply publishes afresh, so it can differ if the data changed meanwhile.'
        return
    }

    if ($refused) {
        Write-RefusalSummary -PublisherLines $publisherLines.ToArray()
        throw $refusalMessage
    }

    if ($changeCount -eq 0) {
        Write-Host 'No changes detected after publishing. Nothing was committed.'
        return
    }

    # The repo must still be what the publish was based on.
    $repoHashesNow = Get-JsonHashes -Folder $repoRoot
    $repoMoved = ($repoHashesNow.Count -ne $repoHashes.Count) -or
        @($repoHashes.Keys | Where-Object { -not $repoHashesNow.ContainsKey($_) -or $repoHashesNow[$_] -ne $repoHashes[$_] }).Count -gt 0
    if ($repoMoved) {
        throw "The repo's JSON files changed while publishing; nothing was copied. Run again."
    }

    # From here the repo is changed. Any failure before the commit is made, or by the commit itself (an index.lock,
    # no user.name / user.email, a hook), prints how to put the repo back before rethrowing.
    try {
        Write-Host 'Copying the published files into the repo...'
        foreach ($name in $removedFiles) {
            Remove-Item -LiteralPath (Join-Path $repoRoot $name) -Force
        }

        foreach ($name in ($addedFiles + $changedFiles)) {
            Copy-Item -LiteralPath (Join-Path $workFolderPath $name) -Destination (Join-Path $repoRoot $name) -Force
        }

        $repoAfter = Get-JsonHashes -Folder $repoRoot
        $copyDiffers = ($repoAfter.Count -ne $workHashes.Count) -or
            @($workHashes.Keys | Where-Object { -not $repoAfter.ContainsKey($_) -or $repoAfter[$_] -ne $workHashes[$_] }).Count -gt 0
        if ($copyDiffers) {
            throw "The repo's JSON files do not match the working folder after copying. Nothing was staged or committed."
        }

        Write-Host "Staging changes (top-level *.json)..."
        Invoke-ExternalCommand -FilePath 'git' -Arguments @('add', '-A', '--', ':(glob)*.json') -Description 'Staging git changes' | Out-Null

        $nameStatus = @(Invoke-ExternalCommand -FilePath 'git' -Arguments @('-c', 'core.quotepath=false', 'diff', '--cached', '-M', '--name-status') -CaptureOutput -Description 'Checking staged changes' | Where-Object { $_ })

        if ($nameStatus.Count -eq 0) {
            Write-Host 'No changes detected after publishing. Nothing was committed.'
            return
        }

        Write-MigrationGitReport -Rows @(Get-MigrationGitRows -Migrations $migrations -NameStatus $nameStatus) -Label 'staged: git diff --cached -M --name-status'

        Write-Host "Creating commit: $commitMessage"
        Invoke-ExternalCommand -FilePath 'git' -Arguments @('commit', '-m', $commitMessage) -Description 'Creating git commit' | Out-Null
    }
    catch {
        Write-ApplyRecovery -PreSha $preSha
        throw
    }

    Write-ApplyCommitGuide -PreSha $preSha
}
finally {
    Pop-Location
}
