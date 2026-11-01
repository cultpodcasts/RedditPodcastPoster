#Requires -Version 7
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0' }
<#
  Offline tests for scripts/add-issue-to-features-board.ps1 (#1043).
  A fake `gh` function stands in for the GitHub CLI (functions win over applications for `& gh`),
  serving synthetic Projects V2 REST responses. Nothing calls GitHub.
#>

BeforeAll {
    $script:ScriptPath = Join-Path $PSScriptRoot '..' 'add-issue-to-features-board.ps1'
    $script:StatusFieldId = 90000001
    $script:IssueUrl = 'https://github.com/synthetic-owner/synthetic-repo/issues/4242'

    function script:New-FakeGh {
        param(
            # JSON body the fake returns for the item verification GET.
            [Parameter(Mandatory)] [string] $ItemGetJson
        )
        $global:FakeGhCalls = [System.Collections.Generic.List[string]]::new()
        $global:FakeGhItemGetJson = $ItemGetJson
        $global:FakeGhBoardListed = $false
        $global:FakeGhStatusFieldId = $script:StatusFieldId

        function global:gh {
            $line = $args -join ' '
            $global:FakeGhCalls.Add($line)
            $global:LASTEXITCODE = 0
            switch -Regex ($line) {
                '^api /repos/synthetic-owner/synthetic-repo/issues/4242 ' {
                    return '{"id":7000001,"node_id":"I_synthetic","title":"Synthetic issue"}'
                }
                '^api /users/cultpodcasts/projectsV2/1/items\?per_page=100 ' {
                    if ($global:FakeGhBoardListed) { return '[4242]' }
                    return '[]'
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

    function script:Invoke-BoardScript {
        & $script:ScriptPath -IssueUrl $script:IssueUrl -TimeoutSeconds 0 -PollSeconds 0 6>$null
    }
}

Describe 'add-issue-to-features-board.ps1 Status verification' {
    AfterEach {
        Remove-Item function:global:gh -ErrorAction SilentlyContinue
    }

    It 'requests the Status field with fields[] on the item GET and exits 0 when Status=Todo persisted' {
        New-FakeGh -ItemGetJson @"
{"id":5550001,"fields":[{"id":$script:StatusFieldId,"name":"Status","data_type":"single_select",
 "value":{"id":"opt-todo","name":{"html":"Todo","raw":"Todo"},"color":"GREEN"}}]}
"@
        Invoke-BoardScript

        $LASTEXITCODE | Should -Be 0
        $global:FakeGhCalls | Should -Contain "api /users/cultpodcasts/projectsV2/1/items/5550001?fields[]=$script:StatusFieldId"
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
}
