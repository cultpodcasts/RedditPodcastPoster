#Requires -Version 7
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0' }
<#
  Offline tests for scripts/add-issue-to-features-board.ps1 (#1043, #1224).
  A fake `gh` function stands in for the GitHub CLI (functions win over applications for `& gh`),
  serving synthetic Projects V2 REST responses. Nothing calls GitHub.
#>

BeforeAll {
    $script:ScriptPath = Join-Path $PSScriptRoot '..' 'add-issue-to-features-board.ps1'
    $script:StatusFieldId = 90000001
    $script:IssueUrl = 'https://github.com/synthetic-owner/synthetic-repo/issues/4242'

    # Synthetic JSON for an item GET whose Status option is Todo.
    $script:TodoItemJson = @"
{"id":5550001,"fields":[{"id":$script:StatusFieldId,"name":"Status","data_type":"single_select",
 "value":{"id":"opt-todo","name":{"html":"Todo","raw":"Todo"},"color":"GREEN"}}]}
"@

    function script:New-FakeGh {
        param(
            # JSON body the fake returns for the item verification GET.
            [Parameter(Mandatory)] [string] $ItemGetJson,
            # Board-list pages served before the item GET (each page is an array of issue numbers).
            [object[]] $PagesBefore = @(, @()),
            # Board-list pages served after the item GET (default: the issue on page 1).
            [object[]] $PagesAfter = @(, @(4242)),
            [switch] $PatchFails
        )
        $global:FakeGhCalls = [System.Collections.Generic.List[string]]::new()
        $global:FakeGhItemGetJson = $ItemGetJson
        $global:FakeGhBoardListed = $false
        $global:FakeGhStatusFieldId = $script:StatusFieldId
        $global:FakeGhPagesBefore = $PagesBefore
        $global:FakeGhPagesAfter = $PagesAfter
        $global:FakeGhPatchFails = [bool]$PatchFails

        function global:gh {
            $line = $args -join ' '
            $global:FakeGhCalls.Add($line)
            $global:LASTEXITCODE = 0
            switch -Regex ($line) {
                '^api /repos/synthetic-owner/synthetic-repo/issues/4242 ' {
                    return '{"id":7000001,"node_id":"I_synthetic","title":"Synthetic issue"}'
                }
                '^api --paginate /users/cultpodcasts/projectsV2/1/items\?per_page=100 --jq ' {
                    # gh --paginate with a scalar --jq prints one value per line across all pages.
                    $pages = if ($global:FakeGhBoardListed) { $global:FakeGhPagesAfter } else { $global:FakeGhPagesBefore }
                    return @($pages | ForEach-Object { $_ } | ForEach-Object { "$_" })
                }
                '^api /users/cultpodcasts/projectsV2/1/fields$' {
                    return @"
[{"id":90000000,"name":"Title","data_type":"title"},
 {"id":$($global:FakeGhStatusFieldId),"name":"Status","data_type":"single_select",
  "options":[{"id":"opt-todo","name":{"html":"Todo","raw":"Todo"}},{"id":"opt-done","name":{"html":"Done","raw":"Done"}}]}]
"@
                }
                '^api -X POST /users/cultpodcasts/projectsV2/1/items ' {
                    return '{"id":5550001}'
                }
                '^api -X PATCH /users/cultpodcasts/projectsV2/1/items/5550001 ' {
                    if ($global:FakeGhPatchFails) {
                        $global:LASTEXITCODE = 1
                        return 'fake gh: HTTP 500 synthetic PATCH failure'
                    }
                    return '{}'
                }
                '^api /users/cultpodcasts/projectsV2/1/items/5550001' {
                    $global:FakeGhBoardListed = $true
                    return $global:FakeGhItemGetJson
                }
                default {
                    $global:LASTEXITCODE = 1
                    return "fake gh: unexpected call: $line"
                }
            }
        }
    }

    # Runs the script and returns its host output (Write-Host) as one string.
    function script:Invoke-BoardScript {
        & $script:ScriptPath -IssueUrl $script:IssueUrl -TimeoutSeconds 0 -PollSeconds 0 6>&1 | Out-String
    }

    function script:Get-CallIndex([string] $Pattern, [switch] $Last) {
        $hits = @(for ($i = 0; $i -lt $global:FakeGhCalls.Count; $i++) { if ($global:FakeGhCalls[$i] -match $Pattern) { $i } })
        if ($hits.Count -eq 0) { return -1 }
        if ($Last) { return $hits[-1] }
        return $hits[0]
    }
}

AfterAll {
    Remove-Item -Path Function:\gh -ErrorAction SilentlyContinue
    Remove-Variable -Scope Global -Name FakeGh* -ErrorAction SilentlyContinue
}

Describe 'add-issue-to-features-board.ps1 Status verification' {
    AfterEach {
        Remove-Item -Path Function:\gh -ErrorAction SilentlyContinue
    }

    It 'requests the Status field with fields[] on the item GET and reaches the success exit when Status=Todo persisted' {
        New-FakeGh -ItemGetJson $script:TodoItemJson
        $output = Invoke-BoardScript

        $LASTEXITCODE | Should -Be 0
        $output | Should -Match 'OK: #4242 is on https://github.com/users/cultpodcasts/projects/1 \(Todo\)'
        $global:FakeGhCalls | Should -Contain "api /users/cultpodcasts/projectsV2/1/items/5550001?fields[]=$script:StatusFieldId"
        $itemGet = Get-CallIndex 'items/5550001\?fields'
        $lastList = Get-CallIndex '^api --paginate .*/items\?per_page=100' -Last
        $lastList | Should -BeGreaterThan $itemGet
    }

    It 'fails with an explicit message, not a StrictMode property error, when the response has only Title' {
        New-FakeGh -ItemGetJson '{"id":5550001,"fields":[{"id":90000000,"name":"Title","data_type":"title","value":{"html":"Synthetic issue","raw":"Synthetic issue"}}]}'

        { Invoke-BoardScript } | Should -Throw '*Status field missing from response*'
    }

    It 'reports a different persisted status as "Status did not persist"' {
        New-FakeGh -ItemGetJson @"
{"id":5550001,"fields":[{"id":$script:StatusFieldId,"name":"Status","data_type":"single_select",
 "value":{"id":"opt-done","name":{"html":"Done","raw":"Done"}}}]}
"@
        { Invoke-BoardScript } | Should -Throw "*Status did not persist (got 'Done')*"
    }

    It 'reports an unset Status value as "Status did not persist" rather than throwing on a null value' {
        New-FakeGh -ItemGetJson "{`"id`":5550001,`"fields`":[{`"id`":$script:StatusFieldId,`"name`":`"Status`",`"data_type`":`"single_select`",`"value`":null}]}"

        { Invoke-BoardScript } | Should -Throw "*Status did not persist (got '')*"
    }

    It 'accepts a legacy plain-string option name in value.name' {
        New-FakeGh -ItemGetJson @"
{"id":5550001,"fields":[{"id":$script:StatusFieldId,"name":"Status","data_type":"single_select",
 "value":{"id":"opt-todo","name":"Todo"}}]}
"@
        $output = Invoke-BoardScript

        $LASTEXITCODE | Should -Be 0
        $output | Should -Match 'Item persisted with Status=Todo'
    }

    It 'matches the Status field by id alone when its name differs' {
        New-FakeGh -ItemGetJson @"
{"id":5550001,"fields":[{"id":$script:StatusFieldId,"name":"Stage","data_type":"single_select",
 "value":{"id":"opt-todo","name":{"html":"Todo","raw":"Todo"}}}]}
"@
        $output = Invoke-BoardScript

        $LASTEXITCODE | Should -Be 0
        $output | Should -Match 'Item persisted with Status=Todo'
    }

    It 'fails with a PATCH-specific message when the Status PATCH fails' {
        New-FakeGh -ItemGetJson $script:TodoItemJson -PatchFails

        { Invoke-BoardScript } | Should -Throw '*Status PATCH failed for item 5550001*'
        $global:FakeGhCalls | Should -Not -Contain "api /users/cultpodcasts/projectsV2/1/items/5550001?fields[]=$script:StatusFieldId"
    }
}

Describe 'add-issue-to-features-board.ps1 board list paging (#1224)' {
    AfterEach {
        Remove-Item -Path Function:\gh -ErrorAction SilentlyContinue
    }

    It 'reads the board list with --paginate' {
        New-FakeGh -ItemGetJson $script:TodoItemJson
        $null = Invoke-BoardScript

        Get-CallIndex '^api --paginate /users/cultpodcasts/projectsV2/1/items\?per_page=100 --jq ' | Should -BeGreaterOrEqual 0
        @($global:FakeGhCalls | Where-Object { $_ -match 'items\?per_page=100' -and $_ -notmatch '--paginate' }).Count | Should -Be 0
    }

    It 'treats an issue listed only on page 2 as already visible: exits 0 with no add and no ghost delete' {
        New-FakeGh -ItemGetJson $script:TodoItemJson -PagesBefore @(@(1..100), @(101, 4242))
        $output = Invoke-BoardScript

        $LASTEXITCODE | Should -Be 0
        $output | Should -Match 'Already visible on the board'
        @($global:FakeGhCalls | Where-Object { $_ -match '-X POST|graphql|-X PATCH' }).Count | Should -Be 0
    }

    It 'finds a newly added issue on page 2 in the post-add poll and exits 0' {
        New-FakeGh -ItemGetJson $script:TodoItemJson -PagesBefore @(@(1..100), @(101, 102)) -PagesAfter @(@(1..100), @(101, 102, 4242))
        $output = Invoke-BoardScript

        $LASTEXITCODE | Should -Be 0
        $output | Should -Match 'OK: #4242 is on'
    }

    It 'ignores non-numeric lines (drafts emit nothing; stray text is skipped)' {
        New-FakeGh -ItemGetJson $script:TodoItemJson -PagesBefore @(, @('', 'not-a-number', '4242'))
        $output = Invoke-BoardScript

        $LASTEXITCODE | Should -Be 0
        $output | Should -Match 'Already visible on the board'
    }
}
