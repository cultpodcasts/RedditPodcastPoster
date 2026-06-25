# YouTube API keys — storage, deploy, and local dev

How YouTube Data API keys are named, stored, and applied across **Azure Key Vault** (deploy-time source), **Function app settings** (literal values at runtime), and **dotnet user-secrets** (local development).

Canonical production layout: [`Infrastructure/functions.bicep`](../Infrastructure/functions.bicep) (`youtube` + `youTubeKeyUsage`).

## Architecture — how keys flow

```
Key Vault (cultpodcasts-deployment)
    │  deploy time only — functions.bicepparam reads via az.getSecret()
    ▼
functions.bicep @secure() params → literal app settings on Function apps
    │  e.g. youtube__Applications__13__ApiKey = <actual key string>
    ▼
YouTubeSettings (IOptions) ← IConfiguration / app settings only
```

| Layer | Key Vault? | Notes |
|-------|------------|-------|
| `functions.bicep` | No references | `@secure()` params become **literal** app-setting values — not `@Microsoft.KeyVault(...)` URIs |
| `functions.bicepparam` | Yes, deploy time | `az.getSecret()` reads KV during `az deployment` / GitHub Actions provision |
| Application code | **Never** | `YouTubeSettings` binds from `youtube` configuration section only (`BindConfiguration<YouTubeSettings>("youtube")`) |
| Interim manual apply | No | Set literal values on Function apps via portal, `az functionapp config appsettings set`, or `apply-youtube-keys.ps1` |

Key Vault is the **authoritative secret store for deploy pipelines**. Storing keys there does **not** mean the running Function app resolves them from Key Vault — the app only sees whatever literal string is in its app settings.

## Azure resources

| Item | Value |
|------|--------|
| Key Vault | `cultpodcasts-deployment` |
| Key Vault resource group | `Management` |
| Function apps resource group | `AutomatedInfra` |
| Function apps | `indexer-infra`, `discover-infra`, `api-infra` |

Bicep reads secrets via `functions.bicepparam` using env vars `AZURE_KEYVAULT_NAME`, `MANAGEMENT_RESOURCEGROUP_NAME`, and `INPUT_SUBSCRIPTION-ID` (set in GitHub Actions `deploy.yml`).

## Key Vault secret naming

YouTube keys use **Pascal-case prefix** `Youtube-ApiKey-N` where `N` is the vault slot (0–16):

| KV secret | Bicep param | App slot (`youtube__Applications__N__*`) | Role |
|-----------|-------------|------------------------------------------|------|
| `Youtube-ApiKey-0` … `Youtube-ApiKey-12` | `youTubeApiKey0` … `youTubeApiKey12` | 0–12 | Cli, Indexer ring, Discover, Bluesky, Api |
| `Youtube-ApiKey-13` | `youTubeApiKey13` | *(legacy — no longer wired)* | Former legacy discover key; safe to leave in vault |
| `Youtube-ApiKey-14` | `youTubeApiKey14` | 14, 16 | Indexer ring keys 10 and 12 (slot 16 dedupes with 14) |
| **`Youtube-ApiKey-15`** | **`youTubeApiKey15`** | **13** | **Indexer ring key 09 (cultpodcasts project)** |
| **`Youtube-ApiKey-16`** | **`youTubeApiKey16`** | **15** | **Indexer ring key 11 (cultpodcasts project)** |

Slots 13 and 15 use lowercase **`cultpodcasts`** as the Google Cloud project id (`Name` field); other slots keep `CultPodcasts`.

### DisplayName scheme (Indexer)

All indexer keys form **one flat rotation ring**. Names are sequential by config slot order — there are no hour-window or reattempt tiers.

| App slot | DisplayName | Ring order |
|----------|-------------|------------|
| 1–4 | `Indexer-Key-01` … `Indexer-Key-04` | 1–4 |
| 8–11 | `Indexer-Key-05` … `Indexer-Key-08` | 5–8 |
| 13 | `Indexer-Key-09-CultPodcasts` | 9 |
| 14 | `Indexer-Key-10-CultPodcasts` | 10 |
| 15 | `Indexer-Key-11-CultPodcasts` | 11 |
| 16 | `Indexer-Key-12-CultPodcasts` | 12 (deduped if same ApiKey as slot 14) |

At runtime the indexer walks this ring in config order (deduped by `ApiKey`), rotating on quota exhaustion. Session resume uses persisted `YouTubeIndexerKeyState`; when no state exists, start position spreads by UTC hour (`hour * ringCount / 24 % ringCount`).

The `Reattempt` field on `Application` is unused for Indexer (kept in schema for backward compatibility).

## Step 1 — Store keys in Key Vault (optional, for deploy pipeline)

Store secrets in Key Vault so `functions.bicepparam` can read them when bicep deploy works again. This is **not** runtime resolution — it is the deploy-time source of truth.

```powershell
az login

$vault = 'cultpodcasts-deployment'

# Replace placeholders with API keys from Google Cloud Console
az keyvault secret set --vault-name $vault --name 'Youtube-ApiKey-15' --value 'YOUR_KEY_FOR_INDEXER_KEY_09'
az keyvault secret set --vault-name $vault --name 'Youtube-ApiKey-16' --value 'YOUR_KEY_FOR_INDEXER_KEY_11'
```

Verify:

```powershell
az keyvault secret list --vault-name cultpodcasts-deployment --query "[?starts_with(name, 'Youtube')].name" -o table
```

## Step 2 — Apply to live Function apps (bicep deploy offline)

Set **literal** API key values on app settings. Do **not** use `@Microsoft.KeyVault(...)` reference URIs.

### All three function apps — script (recommended)

From repo root:

```powershell
# Full apply from Key Vault (keys + DisplayNames)
.\scripts\apply-youtube-keys.ps1 -FromKeyVault

# Display names only (no API key values)
.\scripts\apply-youtube-keys.ps1 -DisplayNamesOnly
```

Legacy wrapper (same as `-DisplayNamesOnly`):

```powershell
.\scripts\apply-youtube-display-names.ps1
```

Each function app restarts after settings change.

### indexer-infra — manual `az` (slots 13 and 15 only)

```powershell
az login

$rg = 'AutomatedInfra'
$app = 'indexer-infra'

az functionapp config appsettings set `
  --resource-group $rg `
  --name $app `
  --settings `
    'youtube__Applications__13__ApiKey=YOUR_KEY' `
    'youtube__Applications__13__Name=cultpodcasts' `
    'youtube__Applications__13__DisplayName=Indexer-Key-09-CultPodcasts' `
    'youtube__Applications__15__ApiKey=YOUR_KEY' `
    'youtube__Applications__15__Name=cultpodcasts' `
    'youtube__Applications__15__DisplayName=Indexer-Key-11-CultPodcasts'
```

## Step 3 — Local dotnet development (user-secrets)

**Never put YouTube API keys in `local.settings.json`.** API keys belong in **dotnet user-secrets** (local) or **literal app settings** (Azure).

All console apps and Cloud function projects share `UserSecretsId` **`e4eaaf12-4507-4875-857d-a8d4032107f3`**. For Indexer local runs, use:

```powershell
$proj = 'Cloud/Indexer/Indexer.csproj'
```

### Example — set cultpodcasts indexer keys

```powershell
$proj = 'Cloud/Indexer/Indexer.csproj'

dotnet user-secrets set "youtube:Applications:13:ApiKey" "YOUR_KEY" --project $proj
dotnet user-secrets set "youtube:Applications:13:Name" "cultpodcasts" --project $proj
dotnet user-secrets set "youtube:Applications:13:DisplayName" "Indexer-Key-09-CultPodcasts" --project $proj

dotnet user-secrets set "youtube:Applications:15:ApiKey" "YOUR_KEY" --project $proj
dotnet user-secrets set "youtube:Applications:15:Name" "cultpodcasts" --project $proj
dotnet user-secrets set "youtube:Applications:15:DisplayName" "Indexer-Key-11-CultPodcasts" --project $proj
```

## Step 4 — When bicep deploys work again

No manual step if Key Vault already has all `Youtube-ApiKey-*` secrets. CI / `functions.bicep` provision writes literal values and flat `Indexer-Key-NN` DisplayNames.

## Slot map (quick reference)

```
Slot  Usage      DisplayName
----  ---------  ---------------------------
0     Cli        ApiKey-0 - Cli
1-4   Indexer    Indexer-Key-01 .. 04
5-6   Discover   ApiKey-5/6 - Discover
7     Bluesky    ApiKey-7 - Bluesky
8-11  Indexer    Indexer-Key-05 .. 08
12    Api        ApiKey-12 - Api
13    Indexer    Indexer-Key-09  ← KV Youtube-ApiKey-15
14    Indexer    Indexer-Key-10  ← KV Youtube-ApiKey-14
15    Indexer    Indexer-Key-11  ← KV Youtube-ApiKey-16
16    Indexer    Indexer-Key-12  ← KV Youtube-ApiKey-14 (shared)
```

## Related

- [README — Configuration](../README.md#configuration)
- [interim-deployment.md](interim-deployment.md) — when GitHub Actions / bicep provision is offline
- [indexing-app-insights-queries.md](indexing-app-insights-queries.md) — indexer key ring telemetry
- [indexer-youtube-go-live.md](indexer-youtube-go-live.md) — production go-live checklist
