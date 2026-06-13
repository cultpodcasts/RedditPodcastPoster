# Deployment guardrails

**Read this before changing anything under `scripts/` or deploying function apps.**

## Source of truth

Production deploys are defined by:

| Artifact | Role |
|----------|------|
| [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml) | Build, test, provision (bicep), deploy all three function apps |
| [`Infrastructure/function.bicep`](../Infrastructure/function.bicep) | Flex Consumption hosting, blob-container deployment target, app settings |
| [`Infrastructure/functions.bicep`](../Infrastructure/functions.bicep) | Per-app settings and module wiring |

**CI does not use local PowerShell scripts.** Agents must not replace or extend the CI mechanism without explicit user approval.

## CI vs local (1:1)

| Step | CI (`deploy.yml`) | Local (`deploy-*.ps1` → `deploy-function-local.ps1`) |
|------|-------------------|-------------------------------------|
| Publish | `dotnet publish -c Release -r linux-x64 <Cloud/...> -o output/<name>` | Same (`-c Release -r linux-x64` → `scripts/.deploy-local/<name>`) |
| Package | Folder artifact (Linux paths; `include-hidden-files: true` for `.azurefunctions/`) | `New-LinuxFunctionAppZip` — forward-slash zip (Windows-only fix) |
| Deploy | `Azure/functions-action@v1` → `package: output/<name>` → `*-infra` | Upload `released-package.zip` to bicep deployment container → `az functionapp restart` |
| App settings | Bicep only | **Never changed** by deploy scripts |
| Provision | Bicep when `Infrastructure/**` changes | Not run locally |

Target apps (resource group `AutomatedInfra`):

| Function | App name | Blob container (bicep) | Blob name (bicep) |
|----------|----------|------------------------|-------------------|
| api | `api-infra` | `api-deployment` | `released-package.zip` |
| discover | `discover-infra` | `discovery-deployment` | `released-package.zip` |
| indexer | `indexer-infra` | `indexer-deployment` | `released-package.zip` |

Bicep configures Flex Consumption (`FC1`) with `functionAppConfig.deployment.storage.type: blobContainer` pointing at `cultpodcastsstg/<container>`. CI's `functions-action` uploads and activates on Linux; local Windows mirrors the same outcome via blob upload + restart.

## Single command

```powershell
az login
.\scripts\deploy-api.ps1
.\scripts\deploy-discover.ps1
.\scripts\deploy-indexer.ps1
```

Each wrapper resolves Azure target details (resource group, app name, storage account, deployment container) then forwards to `deploy-function-local.ps1` with the matching `-FunctionName`. Other parameters (`-SkipPackaging`, `-NoRestore`, `-DeploymentMode`, etc.) pass through unchanged.

### Saved JSON config (per app)

| Wrapper | Config file (gitignored) |
|---------|--------------------------|
| `deploy-api.ps1` | `scripts/deploy-api.json` |
| `deploy-discover.ps1` | `scripts/deploy-discover.json` |
| `deploy-indexer.ps1` | `scripts/deploy-indexer.json` |

Each JSON file stores `resourceGroup`, `appName`, `storageAccount`, and `container`. On run:

1. If the JSON exists, the script shows saved values and prompts `Use these details? (Y/n)`.
2. If you accept (or pass all four `-ResourceGroup`, `-AppName`, `-StorageAccount`, `-DeploymentContainer` on the command line), those values are used.
3. Otherwise, interactive `az` prompts list resource groups, apps, storage accounts, and blob containers; choices are saved back to the JSON for the next run.

Preview without deploying: add `-WhatIf`.

When not using saved JSON or explicit parameters, `deploy-function-local.ps1` still applies its own fallbacks (RG `AutomatedInfra`, app `*-infra`, storage `cultpodcastsstg`, bicep deployment container, blob `released-package.zip`).

## Windows zip packaging (required locally)

`Compress-Archive` produces backslash paths in the zip. Linux Flex hosts cannot load `.azurefunctions/` from such packages — this broke `discover-infra` in June 2026.

**Allowed fix:** `New-LinuxFunctionAppZip` in `AzureWebAppDeploy.ps1` — forward-slash paths only, validates `.azurefunctions/` at zip root. This is a **packaging** fix for Windows, not a new deployment mechanism.

**Do not** revert to `Compress-Archive` for function app packages.

## Flex Consumption activation

After uploading to the bicep deployment container, restart the function app:

```powershell
az functionapp restart -g AutomatedInfra -n discover-infra
```

This is what `deploy-function-local.ps1` does in `FlexBlob` mode (default). Proven when OneDeploy paths fail from Windows (`az webapp deploy` / `az functionapp deploy --src-path` can return 502 on these Flex hosts).

### Do not change app settings

Local deploy scripts are **code-only**. App settings come from bicep ([`Infrastructure/functions.bicep`](../Infrastructure/functions.bicep)). Never add `az functionapp config appsettings set` to deploy scripts unless the user explicitly requests it.

## Thin wrapper architecture

User-facing scripts specialize `-FunctionName`, resolve Azure target details via JSON/interactive prompts (`Resolve-DeploySettings.ps1`), then call `deploy-function-local.ps1` with the resolved `-ResourceGroup`, `-AppName`, `-StorageAccount`, and `-DeploymentContainer`.

All shared behaviour (dotnet publish, `New-LinuxFunctionAppZip`, blob upload to the bicep deployment container, `az functionapp restart`) lives in `deploy-function-local.ps1`. Do not duplicate that logic into wrappers.

## Script inventory

| Script | Status |
|--------|--------|
| `deploy-api.ps1` | **User-facing** — thin wrapper → `deploy-function-local.ps1 -FunctionName api` |
| `deploy-discover.ps1` | **User-facing** — thin wrapper → `-FunctionName discover` (**new file**, June 2026 — see note below) |
| `deploy-indexer.ps1` | **User-facing** — thin wrapper → `-FunctionName indexer` |
| `deploy-function-local.ps1` | **Internal** — publish, zip, blob upload, restart (shared by wrappers) |
| `Resolve-DeploySettings.ps1` | **Internal** — JSON load/save and interactive Azure target prompts |
| `AzureWebAppDeploy.ps1` | Helper — `New-LinuxFunctionAppZip` only |
| `publish-console-apps.ps1` | Console tools — not function deploy |
| `upload-discovery-model.ps1` | Upload discovery accept/reject model bundle to `discovery-models` blob container |

### Discovery scorer model (blob)

Infrastructure creates container **`discovery-models`** on `cultpodcastsstg`. Discover function app settings (bicep) point `discover__scorer__*` at `current/` blobs. Upload after training:

```powershell
az login
.\scripts\upload-discovery-model.ps1 `
  -ModelDirectory "C:\path\to\analysis\model"
```

Required blobs under `current/`: `discovery-accept.model.zip`, `discovery-accept.manifest.json`, `model.onnx`, `vocab.txt` (optional: `show-accept-rates.csv`). Deploy infrastructure first if the container does not exist yet. Flip `discover__scorer__Enabled` in bicep to enable auto-hide in production.

| `scripts/.deploy-local/` | Gitignored build artifacts |

### `deploy-discover.ps1` history

`deploy-discover.ps1` was **created in June 2026** as part of the thin-wrapper refactor. A git-history audit (`f6bded6b`) found **no prior version** of this file on `main` — it is not a restore of an older script. The discover app was always deployed via `deploy-function-local.ps1 -FunctionName discover` (or CI); the dedicated wrapper is new for parity with `deploy-api.ps1` and `deploy-indexer.ps1`.

## Session changes vs `main` (June 2026)

| Change | Verdict | Why |
|--------|---------|-----|
| `New-LinuxFunctionAppZip` | **KEEP** | Windows `Compress-Archive` uses backslashes; broke Linux Flex hosts. CI builds on Linux and does not need this. |
| `Compress-Archive` → `New-LinuxFunctionAppZip` in `deploy-function-local.ps1` | **KEEP** | Same reason — only Windows packaging change. |
| `az functionapp restart` after blob upload | **KEEP** | Activates Flex package after blob upload; proven when OneDeploy fails from Windows. |
| Dot-source `AzureWebAppDeploy.ps1` once at script start | **KEEP** | Loads zip helper only. |
| SAS + `Invoke-WebAppZipDeploy` after blob | **REVERTED** | Session bloat; diverged from CI/bicep blob path; Kudu polling unnecessary. |
| Kudu polling / `Invoke-WebAppZipDeploy` in `AzureWebAppDeploy.ps1` | **REVERTED** | Only used by removed legacy scripts; not CI-equivalent. |
| `deploy-api.ps1`, `deploy-discover.ps1`, `deploy-indexer.ps1` | **REWRITTEN** | Thin wrappers with JSON/interactive Azure targets; no duplicate publish/zip logic. |
| `FunctionAppDeploy` mode (`az functionapp deploy --src-path`) | **UNCHANGED** | Pre-existing on `main`; non-default; 502 on Flex from Windows — use `FlexBlob` (default). |

## Agent guardrails

Enforced in [`.cursor/rules/deployment.mdc`](../.cursor/rules/deployment.mdc) (applies when editing `scripts/**/*.ps1` or this file).

1. **Never delete or rename `scripts/deploy-*.ps1`** without explicit user approval. User-facing entry points: `deploy-api.ps1`, `deploy-discover.ps1`, `deploy-indexer.ps1` → internal `deploy-function-local.ps1`.
2. **Do not change the deployment mechanism** (OneDeploy modes, blob upload flow, app settings, Kudu/SAS polling) unless the user explicitly asks.
3. **Do not deploy to production** unless the user explicitly asks.
4. **Prefer CI** — push to `main` and let `deploy.yml` run when Actions billing is healthy.
5. **Compare to `deploy.yml` and bicep first** — local script changes must be justified as "CI equivalent on Windows", not "new Azure integration".
6. **Never modify app settings** during code deploy.
7. **Read this file** before editing `scripts/`.
8. **Document** script changes here when behaviour changes.

### June 2026 session incident

An agent session **wrongly deleted** `deploy-api.ps1` and `deploy-indexer.ps1` as "duplicates" and removed a session-created `deploy-discover.ps1` the user expected. **Never repeat.** All three thin wrappers were restored; see [`deploy-discover.ps1` history](#deploy-discoverps1-history) above.

## CI status check

Before assuming a push fixed production:

```powershell
gh run list --workflow=deploy.yml --limit 3
```

As of 2026-06-11, recent `deploy.yml` runs failed with **GitHub Actions billing lock** — no CI deploy reached Azure. Local deploy or billing resolution is required until Actions runs again.

## Backfill (separate)

Discovery backfill is **not** part of deploy. When ready, see [discovery-backfill.md](discovery-backfill.md).

## Verify after deploy

```powershell
az functionapp function list -g AutomatedInfra -n discover-infra -o table
```

Expected for discover: `DiscoveryTrigger`, `Discover`, `Orchestration`.
