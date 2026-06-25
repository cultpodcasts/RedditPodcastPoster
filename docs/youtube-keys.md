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
| `Youtube-ApiKey-0` … `Youtube-ApiKey-12` | `youTubeApiKey0` … `youTubeApiKey12` | 0–12 | Cli, Indexer primaries, Discover, Bluesky, Reattempt1, Api |
| `Youtube-ApiKey-13` | `youTubeApiKey13` | *(legacy — no longer wired)* | Former cultcodcasts Reattempt2 key; safe to leave in vault |
| `Youtube-ApiKey-14` | `youTubeApiKey14` | 14, 16 | Indexer Reattempt2 hour-windows 2 and 4 |
| **`Youtube-ApiKey-15`** | **`youTubeApiKey15`** | **13** | **Indexer Reattempt2 hour-window 1 (new cultpodcasts key)** |
| **`Youtube-ApiKey-16`** | **`youTubeApiKey16`** | **15** | **Indexer Reattempt2 hour-window 3 (new cultpodcasts key)** |

Slots 13 and 15 previously shared `Youtube-ApiKey-13` under the misspelled `cultcodcasts` application name. The two new keys get dedicated vault slots **15** and **16** and map to app slots **13** and **15**. Their `Name` field is lowercase **`cultpodcasts`** (Google Cloud project id); other slots keep `CultPodcasts`.

### DisplayName scheme (Indexer)

`HourPrimary-N` = UTC hour-window primary for hours `(N-1)*6` through `N*6-1`. All indexer keys share one rotation ring.

| App slot | DisplayName | Reattempt |
|----------|-------------|-----------|
| 1–4 | `Indexer-HourPrimary-{1-4}-CultPodcasts` | — |
| 8–11 | `Indexer-HourPrimary-{1-4}-Reattempt1-CultPodcasts` | 1 |
| 13 | `Indexer-HourPrimary-1-Reattempt2-CultPodcasts` | 2 |
| 14 | `Indexer-HourPrimary-2-Reattempt2-CultPodcasts` | 2 |
| 15 | `Indexer-HourPrimary-3-Reattempt2-CultPodcasts` | 2 |
| 16 | `Indexer-HourPrimary-4-Reattempt2-CultPodcasts` | 2 |

## Step 1 — Store keys in Key Vault (optional, for deploy pipeline)

Store secrets in Key Vault so `functions.bicepparam` can read them when bicep deploy works again. This is **not** runtime resolution — it is the deploy-time source of truth.

```powershell
az login

$vault = 'cultpodcasts-deployment'

# Replace placeholders with your two new cultpodcasts API keys from Google Cloud Console
az keyvault secret set --vault-name $vault --name 'Youtube-ApiKey-15' --value 'YOUR_NEW_KEY_FOR_HOURPRIMARY_1_REATTEMPT2'
az keyvault secret set --vault-name $vault --name 'Youtube-ApiKey-16' --value 'YOUR_NEW_KEY_FOR_HOURPRIMARY_3_REATTEMPT2'
```

Verify:

```powershell
az keyvault secret list --vault-name cultpodcasts-deployment --query "[?starts_with(name, 'Youtube')].name" -o table
```

## Step 2 — Apply to live Function apps (bicep deploy offline)

Set **literal** API key values on app settings. Do **not** use `@Microsoft.KeyVault(...)` reference URIs.

### indexer-infra — manual `az` (recommended)

Replace the placeholders with your actual key strings from Google Cloud Console:

```powershell
az login

$rg = 'AutomatedInfra'
$app = 'indexer-infra'

az functionapp config appsettings set `
  --resource-group $rg `
  --name $app `
  --settings `
    'youtube__Applications__13__ApiKey=YOUR_NEW_KEY_FOR_HOURPRIMARY_1_REATTEMPT2' `
    'youtube__Applications__13__Name=cultpodcasts' `
    'youtube__Applications__13__DisplayName=Indexer-HourPrimary-1-Reattempt2-CultPodcasts' `
    'youtube__Applications__13__Reattempt=2' `
    'youtube__Applications__15__ApiKey=YOUR_NEW_KEY_FOR_HOURPRIMARY_3_REATTEMPT2' `
    'youtube__Applications__15__Name=cultpodcasts' `
    'youtube__Applications__15__DisplayName=Indexer-HourPrimary-3-Reattempt2-CultPodcasts' `
    'youtube__Applications__15__Reattempt=2'
```

Verify (values masked in output):

```powershell
az functionapp config appsettings list `
  --resource-group $rg `
  --name $app `
  --query "[?contains(name, 'youtube__Applications__1') && (contains(name, 'ApiKey') || contains(name, 'Name') || contains(name, 'DisplayName'))].{name:name,value:value}" `
  -o table
```

You can also set these in the Azure Portal under **Configuration → Application settings**. Use literal values, not Key Vault references.

### All three function apps — script

From repo root:

```powershell
# New Reattempt2 keys (slots 13 and 15) + DisplayNames on indexer-infra, discover-infra, api-infra
.\scripts\apply-youtube-keys.ps1 -ApiKey15 'YOUR_KEY' -ApiKey16 'YOUR_KEY' -ApplyNewKeysOnly

# Display names only (no API key values)
.\scripts\apply-youtube-keys.ps1 -DisplayNamesOnly
```

Legacy wrapper (same as `-DisplayNamesOnly`):

```powershell
.\scripts\apply-youtube-display-names.ps1
```

Each function app restarts after settings change.

## Step 3 — Local dotnet development (user-secrets)

**Never put YouTube API keys in `local.settings.json`.** That file is gitignored and intended for non-secret host config only (`AzureWebJobsStorage`, timer disables, `indexer`/`poster` tuning). API keys belong in **dotnet user-secrets** (local) or **literal app settings** (Azure).

All console apps and Cloud function projects share `UserSecretsId` **`e4eaaf12-4507-4875-857d-a8d4032107f3`**. For Indexer local runs, use:

```powershell
$proj = 'Cloud/Indexer/Indexer.csproj'
```

Console apps may use the same store via any project path, e.g. `Console-Apps/Index/Index.csproj`.

### Option A — set the two new keys only

```powershell
$proj = 'Cloud/Indexer/Indexer.csproj'

dotnet user-secrets set "youtube:Applications:13:ApiKey" "YOUR_NEW_KEY_FOR_HOURPRIMARY_1_REATTEMPT2" --project $proj
dotnet user-secrets set "youtube:Applications:13:Name" "cultpodcasts" --project $proj
dotnet user-secrets set "youtube:Applications:13:DisplayName" "Indexer-HourPrimary-1-Reattempt2-CultPodcasts" --project $proj
dotnet user-secrets set "youtube:Applications:13:Reattempt" "2" --project $proj

dotnet user-secrets set "youtube:Applications:15:ApiKey" "YOUR_NEW_KEY_FOR_HOURPRIMARY_3_REATTEMPT2" --project $proj
dotnet user-secrets set "youtube:Applications:15:Name" "cultpodcasts" --project $proj
dotnet user-secrets set "youtube:Applications:15:DisplayName" "Indexer-HourPrimary-3-Reattempt2-CultPodcasts" --project $proj
dotnet user-secrets set "youtube:Applications:15:Reattempt" "2" --project $proj
```

## Step 4 — When bicep deploys work again

No manual step if Key Vault already has `Youtube-ApiKey-15` and `Youtube-ApiKey-16`. CI / `functions.bicep` provision will:

1. Read all `Youtube-ApiKey-*` secrets via `functions.bicepparam` (deploy time)
2. Pass literal values into app settings (`youtube__Applications__13__ApiKey` ← `youTubeApiKey15`, etc.)
3. Apply updated DisplayNames and `cultpodcasts` Name fields

Push to `main` or run the provision job from [`deploy.yml`](../.github/workflows/deploy.yml).

## Slot map (quick reference)

```
Slot  Usage      DisplayName suffix
----  ---------  ------------------------------------------
0     Cli        ApiKey-0 - Cli
1-4   Indexer    HourPrimary-1..4-CultPodcasts
5-6   Discover   ApiKey-5/6 - Discover
7     Bluesky    ApiKey-7 - Bluesky
8-11  Indexer    HourPrimary-1..4-Reattempt1-CultPodcasts
12    Api        ApiKey-12 - Api
13    Indexer    HourPrimary-1-Reattempt2-CultPodcasts  ← KV Youtube-ApiKey-15
14    Indexer    HourPrimary-2-Reattempt2-CultPodcasts  ← KV Youtube-ApiKey-14
15    Indexer    HourPrimary-3-Reattempt2-CultPodcasts  ← KV Youtube-ApiKey-16
16    Indexer    HourPrimary-4-Reattempt2-CultPodcasts  ← KV Youtube-ApiKey-14 (shared)
```

## Related

- [README — Configuration](../README.md#configuration)
- [interim-deployment.md](interim-deployment.md) — when GitHub Actions / bicep provision is offline
- [indexing-app-insights-queries.md](indexing-app-insights-queries.md) — indexer key ring telemetry
