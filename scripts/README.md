Deploy scripts: see [docs/deployment.md](../docs/deployment.md) for architecture, guardrails, and usage.

Script tests: offline Pester tests live in [tests/](tests/) (fake `gh`, no network). Run `Invoke-Pester ./scripts/tests` (Pester 5+); CI runs them in the Build workflow.
