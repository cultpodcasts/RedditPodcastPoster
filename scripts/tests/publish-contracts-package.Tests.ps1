#Requires -Version 7
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot '..' 'publish-contracts-package.ps1')
}

Describe 'contract publish plan' {
    It 'publishes major.minor.0 when the registry has no package' {
        $plan = Get-ContractsPublishPlan -CommittedVersion '0.1.0' -ContentHash 'abc' -PublishedVersions @() -LatestContentHash $null

        $plan.Action | Should -Be 'publish'
        $plan.Version | Should -Be '0.1.0'
    }

    It 'skips when the generated hash matches the latest publish on the same release line' {
        $plan = Get-ContractsPublishPlan -CommittedVersion '0.1.0' -ContentHash 'abc' -PublishedVersions @('0.1.0', '0.1.2') -LatestContentHash 'abc'

        $plan.Action | Should -Be 'skip'
        $plan.Version | Should -Be '0.1.2'
    }

    It 'publishes the next patch when the generated hash differs' {
        $plan = Get-ContractsPublishPlan -CommittedVersion '0.1.0' -ContentHash 'new' -PublishedVersions @('0.1.2', '0.1.4') -LatestContentHash 'old'

        $plan.Action | Should -Be 'publish'
        $plan.Version | Should -Be '0.1.5'
    }

    It 'publishes the next patch when the published package has no content hash' {
        $plan = Get-ContractsPublishPlan -CommittedVersion '0.1.0' -ContentHash 'abc' -PublishedVersions @('0.1.0') -LatestContentHash $null

        $plan.Action | Should -Be 'publish'
        $plan.Version | Should -Be '0.1.1'
    }

    It 'opens a new release line at patch 0 when major or minor moves forward' {
        $plan = Get-ContractsPublishPlan -CommittedVersion '0.2.0' -ContentHash 'abc' -PublishedVersions @('0.1.4') -LatestContentHash 'abc'

        $plan.Action | Should -Be 'publish'
        $plan.Version | Should -Be '0.2.0'
    }

    It 'fails when the committed release line is behind the registry' {
        $plan = Get-ContractsPublishPlan -CommittedVersion '0.1.0' -ContentHash 'abc' -PublishedVersions @('0.2.1') -LatestContentHash 'other'

        $plan.Action | Should -Be 'fail'
    }

    It 'ignores pre-release versions when choosing the next patch' {
        $plan = Get-ContractsPublishPlan -CommittedVersion '0.1.0' -ContentHash 'new' -PublishedVersions @('0.1.2', '0.1.9-beta.1') -LatestContentHash 'old'

        $plan.Action | Should -Be 'publish'
        $plan.Version | Should -Be '0.1.3'
    }
}

Describe 'npm view parsing' {
    It 'treats E404 as an empty registry' {
        $parsed = ConvertFrom-NpmVersionsOutput -ExitCode 1 -Output "npm error code E404`nno such package"

        $parsed.Ok | Should -BeTrue
        $parsed.Versions | Should -BeNullOrEmpty
    }

    It 'fails closed on any other npm view error' {
        $parsed = ConvertFrom-NpmVersionsOutput -ExitCode 1 -Output 'npm error code E401'

        $parsed.Ok | Should -BeFalse
    }

    It 'reads one version or many' {
        (ConvertFrom-NpmVersionsOutput -ExitCode 0 -Output '"0.1.0"').Versions | Should -Be @('0.1.0')
        (ConvertFrom-NpmVersionsOutput -ExitCode 0 -Output '["0.1.0","0.1.1"]').Versions | Should -Be @('0.1.0', '0.1.1')
    }

    It 'reads a hash and treats a missing field as no hash' {
        (ConvertFrom-NpmHashOutput -ExitCode 0 -Output '"abc"').Hash | Should -Be 'abc'
        (ConvertFrom-NpmHashOutput -ExitCode 0 -Output '').Hash | Should -BeNullOrEmpty
        (ConvertFrom-NpmHashOutput -ExitCode 1 -Output 'npm error code E500').Ok | Should -BeFalse
    }
}

Describe 'generated content hash' {
    It 'changes when a generated file changes and ignores carriage returns' {
        $left = Join-Path $TestDrive 'left'
        $right = Join-Path $TestDrive 'right'
        foreach ($dir in @($left, $right)) {
            New-Item -ItemType Directory -Path (Join-Path $dir 'src'), (Join-Path $dir 'schemas') -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $dir 'src/index.ts') -Value "export type A = string;`n" -NoNewline
            Set-Content -LiteralPath (Join-Path $dir 'src/zod.ts') -Value "export const a = 1;`n" -NoNewline
            Set-Content -LiteralPath (Join-Path $dir 'schemas/contracts.schema.json') -Value "{`n}`n" -NoNewline
        }

        $same = Get-ContractsContentHash -Directory $left
        (Get-ContractsContentHash -Directory $right) | Should -Be $same

        Set-Content -LiteralPath (Join-Path $right 'src/index.ts') -Value "export type A = string;`r`n" -NoNewline
        (Get-ContractsContentHash -Directory $right) | Should -Be $same

        Set-Content -LiteralPath (Join-Path $right 'src/zod.ts') -Value "export const a = 2;`n" -NoNewline
        (Get-ContractsContentHash -Directory $right) | Should -Not -Be $same
    }
}

Describe 'publish script' {
    It 'does not publish when the registry already has this hash' {
        $dir = Join-Path $TestDrive 'pkg-skip'
        New-Item -ItemType Directory -Path (Join-Path $dir 'src'), (Join-Path $dir 'schemas') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $dir 'src/index.ts') -Value "export {}`n" -NoNewline
        Set-Content -LiteralPath (Join-Path $dir 'src/zod.ts') -Value "export {}`n" -NoNewline
        Set-Content -LiteralPath (Join-Path $dir 'schemas/contracts.schema.json') -Value "{}`n" -NoNewline
        Set-Content -LiteralPath (Join-Path $dir 'package.json') -Value '{"name":"@example/contracts","version":"0.1.0"}' -NoNewline
        $hash = Get-ContractsContentHash -Directory $dir
        $calls = [System.Collections.Generic.List[string]]::new()
        $fake = {
            $calls.Add(($args -join ' '))
            $global:LASTEXITCODE = 0
            $line = $args -join ' '
            if ($line -match 'versions') { return '["0.1.2"]' }
            if ($line -match 'contractsContentHash') { return ('"' + $hash + '"') }
            return 'unexpected'
        }

        $code = Invoke-ContractsPackagePublish -Directory $dir -Command $fake

        $code | Should -Be 0
        ($calls -join "`n") | Should -Not -Match 'publish'
    }

    It 'sets the next patch and publishes when the hash differs' {
        $dir = Join-Path $TestDrive 'pkg-publish'
        New-Item -ItemType Directory -Path (Join-Path $dir 'src'), (Join-Path $dir 'schemas') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $dir 'src/index.ts') -Value "export {}`n" -NoNewline
        Set-Content -LiteralPath (Join-Path $dir 'src/zod.ts') -Value "export {}`n" -NoNewline
        Set-Content -LiteralPath (Join-Path $dir 'schemas/contracts.schema.json') -Value "{}`n" -NoNewline
        Set-Content -LiteralPath (Join-Path $dir 'package.json') -Value '{"name":"@example/contracts","version":"0.1.0"}' -NoNewline
        $calls = [System.Collections.Generic.List[string]]::new()
        $fake = {
            $calls.Add(($args -join ' '))
            $global:LASTEXITCODE = 0
            $line = $args -join ' '
            if ($line -match 'versions') { return '["0.1.2"]' }
            if ($line -match 'contractsContentHash') { return '"previous"' }
            if ($line -match '^version ') {
                $pkgPath = Join-Path (Get-Location) 'package.json'
                $node = [System.Text.Json.Nodes.JsonNode]::Parse((Get-Content -LiteralPath $pkgPath -Raw))
                $node['version'] = $args[1]
                [System.IO.File]::WriteAllText($pkgPath, $node.ToJsonString())
                return $args[1]
            }
            if ($line -eq 'publish') { return '' }
            $global:LASTEXITCODE = 1
            return "unexpected $line"
        }

        $code = Invoke-ContractsPackagePublish -Directory $dir -Command $fake

        $code | Should -Be 0
        $calls | Should -Contain 'version 0.1.3 --no-git-tag-version'
        $calls | Should -Contain 'publish'
        $written = [System.Text.Json.JsonDocument]::Parse((Get-Content -LiteralPath (Join-Path $dir 'package.json') -Raw)).RootElement
        $written.GetProperty('version').GetString() | Should -Be '0.1.3'
        $written.GetProperty('contractsContentHash').GetString() | Should -Be (Get-ContractsContentHash -Directory $dir)
    }
}
