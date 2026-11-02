Deploy scripts: see [docs/deployment.md](../docs/deployment.md) for architecture, guardrails, and usage.

Script tests: offline Pester tests live in [tests/](tests/) (fake `gh`, no network). Locally run `Invoke-Pester ./scripts/tests` (Pester 5+), or `pwsh -c 'Invoke-Pester ./scripts/tests'` from bash. CI runs the same suite after unit-test guardrails via the `Pester` step in [`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml) (`Invoke-Pester ./scripts/tests -CI`).
