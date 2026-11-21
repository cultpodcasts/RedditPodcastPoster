#Requires -Version 7
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0' }

BeforeAll {
    $script:AssertScript = Join-Path $PSScriptRoot '..' 'assert-contract-package-version.ps1'
}

Describe 'contract release line' {
    It 'accepts major.minor.0' {
        $path = Join-Path $TestDrive 'ok.json'
        Set-Content -LiteralPath $path -Value '{"version":"0.2.0"}' -NoNewline

        $output = & $script:AssertScript -PackageJson $path 6>&1 | Out-String

        $LASTEXITCODE | Should -Be 0
        $output | Should -Match '0\.2\.0'
    }

    It 'rejects a hand-written patch' {
        $path = Join-Path $TestDrive 'patch.json'
        Set-Content -LiteralPath $path -Value '{"version":"0.1.4"}' -NoNewline

        $output = & $script:AssertScript -PackageJson $path 6>&1 | Out-String

        $LASTEXITCODE | Should -Be 1
        $output | Should -Match 'major\.minor\.0'
    }
}
