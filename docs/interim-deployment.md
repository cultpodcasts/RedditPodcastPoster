# Interim deployment (GitHub Actions inactive)

Use this guide when [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml) is not running (e.g. Actions billing lock). PowerShell scripts under `scripts/` mirror **parts** of the CI pipeline and bicep provisioning so you can deploy from a developer machine with `az login`.

**Canonical reference when CI is healthy:** [deployment.md](deployment.md) and `deploy.yml`.

## CI job map → local scripts

| CI (`deploy.yml`) | Bicep / artifact | Interim script | Notes |
|-------------------|------------------|----------------|-------|
| Build + publish matrix | `dotnet publish -r linux-x64` | *(inside `deploy-function-local.ps1`)* | Windows uses `New-LinuxFunctionAppZip` for forward-slash paths |
| `provision` → Storage | [`function-storage.bicep`](../Infrastructure/function-storage.bicep) | [`provision-storage-containers.ps1`](../scripts/provision-storage-containers.ps1) | Containers only; storage account must already exist |
| `provision` → Functions | [`functions.bicep`](../Infrastructure/functions.bicep) | **No script** | App settings (including `discover__scorer__*`) come from bicep only when this deploys |
| `provision` → Cosmos diagnostics | [`cosmos-db-diagnostics.bicep`](../Infrastructure/cosmos-db-diagnostics.bicep) | [`enable-cosmos-diagnostics.ps1`](../scripts/enable-cosmos-diagnostics.ps1) / [`disable-cosmos-diagnostics.ps1`](../scripts/disable-cosmos-diagnostics.ps1) | Temporary RU investigation |
| Telemetry / sampling settings | `jobHostLogging`, `logging`, `memoryProbe` in `functions.bicep` | [`apply-telemetry-app-settings.ps1`](../scripts/apply-telemetry-app-settings.ps1) | **Exception:** patches app settings when bicep is not deploying |
| `api-deploy` / `discover-deploy` / `indexer-deploy` | Flex blob package | [`deploy-api.ps1`](../scripts/deploy-api.ps1), [`deploy-discover.ps1`](../scripts/deploy-discover.ps1), [`deploy-indexer.ps1`](../scripts/deploy-indexer.ps1) | Code-only; no app settings |
| Console tools (not in CI) | — | [`publish-console-apps.ps1`](../scripts/publish-console-apps.ps1) | Local Windows executables |
| Discovery ML model upload (not in CI) | `discovery-models` container | [`upload-discovery-model.ps1`](../scripts/upload-discovery-model.ps1) | After training; requires container |

Shared internals: [`deploy-function-local.ps1`](../scripts/deploy-function-local.ps1), [`Resolve-DeploySettings.ps1`](../scripts/Resolve-DeploySettings.ps1), [`AzureWebAppDeploy.ps1`](../scripts/AzureWebAppDeploy.ps1).

## Typical interim flow

```powershell
az login

# 1. Storage containers (if new container added in bicep, e.g. discovery-models)
.\scripts\provision-storage-containers.ps1

# 2. Code deploy (pick one or all)
.\scripts\deploy-discover.ps1

# 3. Discovery scorer model (after training, if using ML auto-hide)
.\scripts\upload-discovery-model.ps1 -ModelDirectory "C:\path\to\analysis\model"
.\scripts\apply-discover-scorer-settings.ps1

# 4. Verify
az functionapp function list -g AutomatedInfra -n discover-infra -o table
```

Function deploy scripts **do not** apply bicep app settings. If you added new settings in `functions.bicep` (e.g. `discover__scorer__Enabled`), you must either run a bicep deploy for the functions template or apply those keys manually once via Azure Portal / `az functionapp config appsettings set` on `discover-infra`.

## Azure targets (defaults)

| Setting | Default |
|---------|---------|
| Resource group | `AutomatedInfra` |
| Function apps | `api-infra`, `discover-infra`, `indexer-infra` |
| Storage account | `cultpodcastsstg` |
| Deployment containers | `api-deployment`, `discovery-deployment`, `indexer-deployment` |
| Deployment blob | `released-package.zip` |
| Model container | `discovery-models` / prefix `current/` |

Per-app overrides: gitignored `scripts/deploy-api.json`, `deploy-discover.json`, `deploy-indexer.json` (saved on first interactive run).

## Where configuration comes from (local runs)

### Azure Functions (production / `discover-infra`)

| Source | Used for |
|--------|----------|
| [`Infrastructure/functions.bicep`](../Infrastructure/functions.bicep) | All production app settings (`discover__*`, `cosmosdb__*`, secrets from Key Vault at deploy time) |
| Function app settings in Azure Portal | What is actually running until next bicep provision |
| [`apply-telemetry-app-settings.ps1`](../scripts/apply-telemetry-app-settings.ps1) | Optional overlay for logging/sampling when bicep provision is skipped |

Function apps do **not** read repo `appsettings.json`. Local debugging of `Cloud/Discovery` uses [`Cloud/Discovery/Properties/launchSettings.json`](../Cloud/Discovery/Properties/launchSettings.json) environment variables (subset of bicep `discover__*`).

### Console apps (`Discover.exe`, training tools, etc.)

| Source | Used for |
|--------|----------|
| **User secrets** | Cosmos, Spotify, Reddit, API keys — shared `UserSecretsId` across console projects; set via `dotnet user-secrets set` (see [README](../README.md)) |
| `RedditPodcastPoster_*` environment variables | Same keys as secrets, alternative to user secrets |
| [`Console-Apps/Discover/appsettings.json`](../Console-Apps/Discover/appsettings.json) | Queries, ignore terms, local `discover:scorer` (disabled by default) |
| `Discover.appsettings.json` next to published exe | Copied by `publish-console-apps.ps1` into `artifacts/tools/` |
| CLI arguments | Backfill window (`--time-since`), service flags — see [discovery-backfill.md](discovery-backfill.md) |

Production-equivalent discover queries / ignore terms live in bicep; keep local `appsettings.json` in sync when testing behaviour that depends on them.

### Secrets → function app setting names

```powershell
dotnet run --project Console-Apps/MigrateConfig -- secrets path-to-secrets.json
```

Converts user-secrets JSON (`section:key`) to Azure format (`section__key`).

## Script reference (2026)

| Script | Added / purpose |
|--------|-----------------|
| `deploy-api.ps1`, `deploy-indexer.ps1`, `deploy-discover.ps1` | June 2026 — thin wrappers over `deploy-function-local.ps1` |
| `deploy-function-local.ps1` | Shared publish, Linux zip, blob upload, restart |
| `Resolve-DeploySettings.ps1` | JSON + interactive Azure target resolution |
| `AzureWebAppDeploy.ps1` | `New-LinuxFunctionAppZip` (Windows packaging fix) |
| `apply-telemetry-app-settings.ps1` | Interim telemetry/sampling when bicep not deploying |
| `enable-cosmos-diagnostics.ps1` / `disable-cosmos-diagnostics.ps1` | Interim Cosmos diagnostic export |
| `publish-console-apps.ps1` | Self-contained Windows console tools |
| `provision-storage-containers.ps1` | Interim `function-storage.bicep` container list |
| `upload-discovery-model.ps1` | Upload ML scorer bundle to `discovery-models` |
| `apply-discover-scorer-settings.ps1` | Apply `discover__scorer__*` on `discover-infra` when bicep not deploying |
| `apply-youtube-keys.ps1` | Apply YouTube API keys + DisplayNames when bicep not deploying (see [youtube-keys.md](youtube-keys.md)) |
| `apply-youtube-display-names.ps1` | DisplayNames only (wrapper over `apply-youtube-keys.ps1`) |

Build artifacts (gitignored): `scripts/.deploy-local/`, `artifacts/tools/`, `artifacts/.console-publish-staging/`.

## Discovery scorer checklist (interim)

1. `provision-storage-containers.ps1` — ensures `discovery-models` exists  
2. Train model locally (`DiscoveryTrainingTrain`)  
3. `upload-discovery-model.ps1` — uploads to `current/`  
4. Ensure `discover__scorer__*` app settings exist on `discover-infra` (bicep deploy or [`apply-discover-scorer-settings.ps1`](../scripts/apply-discover-scorer-settings.ps1))  
5. `deploy-discover.ps1` — deploy code that reads model from blob  
6. Confirm logs on next run: model sync + auto-hide messages  

## When CI returns

Prefer pushing to `main` and letting `deploy.yml` provision and deploy. Keep interim scripts for emergency code pushes and one-off infra gaps (new blob container before next bicep run).

Check CI status:

```powershell
gh run list --workflow=deploy.yml --limit 3
```

## Related docs

- [deployment.md](deployment.md) — guardrails, CI vs local parity, agent rules  
- [discovery-backfill.md](discovery-backfill.md) — local `Discover.exe` backfill  
- [README.md](../README.md) — user secrets and configuration overview  
