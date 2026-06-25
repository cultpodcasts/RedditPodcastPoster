# YouTube API keys — Key Vault, local dev, and interim Azure

How YouTube Data API keys are named, stored, and applied across **Azure Key Vault**, **Function app settings**, and **dotnet user-secrets** for local development.

Canonical production layout: [`Infrastructure/functions.bicep`](../Infrastructure/functions.bicep) (`youtube` + `youTubeKeyUsage`).

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

Slots 13 and 15 previously shared `Youtube-ApiKey-13` under the misspelled `cultcodcasts` application name. The two new keys get dedicated vault slots **15** and **16** and map to app slots **13** and **15**.

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

## Step 1 — Add new keys to Key Vault (now)

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

From repo root:

```powershell
# Reads Youtube-ApiKey-15 and -16 from Key Vault + updated DisplayNames (recommended)
.\scripts\apply-youtube-keys.ps1 -FromKeyVault -ApplyNewKeysOnly

# Or read the full key ring (Youtube-ApiKey-0..16) from Key Vault
.\scripts\apply-youtube-keys.ps1 -FromKeyVault

# Display names only (no API key values)
.\scripts\apply-youtube-keys.ps1 -DisplayNamesOnly
```

Do **not** paste API key values on the command line in shared shells. Prefer `-FromKeyVault` so values are read via `az keyvault secret show` only.

Legacy wrapper (same as `-DisplayNamesOnly`):

```powershell
.\scripts\apply-youtube-display-names.ps1
```

Each function app restarts after settings change.

## Step 3 — Local dotnet development (user-secrets)

**Never put YouTube API keys in `local.settings.json`.** That file is gitignored and intended for non-secret host config only (`AzureWebJobsStorage`, timer disables, `indexer`/`poster` tuning). API keys belong in **dotnet user-secrets** (local) or **Key Vault → app settings** (Azure).

All console apps and Cloud function projects share `UserSecretsId` **`e4eaaf12-4507-4875-857d-a8d4032107f3`**. For Indexer local runs, use:

```powershell
$proj = 'Cloud/Indexer/Indexer.csproj'
```

Console apps may use the same store via any project path, e.g. `Console-Apps/Index/Index.csproj`.

Template (no real keys): [`secrets/youtube-keys.template.json`](../secrets/youtube-keys.template.json). Array indices **must** match bicep app slots 0–16. Copy to a gitignored path such as `secrets/youtube-keys.local.json` before filling in placeholders.

### Option A — set the two new keys only

```powershell
$proj = 'Cloud/Indexer/Indexer.csproj'

dotnet user-secrets set "youtube:Applications:13:ApiKey" "YOUR_NEW_KEY_FOR_HOURPRIMARY_1_REATTEMPT2" --project $proj
dotnet user-secrets set "youtube:Applications:13:Name" "CultPodcasts" --project $proj
dotnet user-secrets set "youtube:Applications:13:DisplayName" "Indexer-HourPrimary-1-Reattempt2-CultPodcasts" --project $proj
dotnet user-secrets set "youtube:Applications:13:Reattempt" "2" --project $proj

dotnet user-secrets set "youtube:Applications:15:ApiKey" "YOUR_NEW_KEY_FOR_HOURPRIMARY_3_REATTEMPT2" --project $proj
dotnet user-secrets set "youtube:Applications:15:Name" "CultPodcasts" --project $proj
dotnet user-secrets set "youtube:Applications:15:DisplayName" "Indexer-HourPrimary-3-Reattempt2-CultPodcasts" --project $proj
dotnet user-secrets set "youtube:Applications:15:Reattempt" "2" --project $proj
```

### Option B — bulk import from JSON

1. Copy `secrets/youtube-keys.template.json` to a **gitignored** path (e.g. `secrets/youtube-keys.local.json`).
2. Fill in all `YOUR_YOUTUBE_API_KEY_*` placeholders with real values.
3. Merge into user-secrets (PowerShell example):

```powershell
$local = Get-Content secrets/youtube-keys.local.json -Raw | ConvertFrom-Json
$proj = 'Cloud/Indexer/Indexer.csproj'
for ($i = 0; $i -lt $local.youtube.Applications.Count; $i++) {
    $app = $local.youtube.Applications[$i]
    dotnet user-secrets set "youtube:Applications:$i`:ApiKey" $app.ApiKey --project $proj
    dotnet user-secrets set "youtube:Applications:$i`:Name" $app.Name --project $proj
    dotnet user-secrets set "youtube:Applications:$i`:Usage" $app.Usage --project $proj
    dotnet user-secrets set "youtube:Applications:$i`:DisplayName" $app.DisplayName --project $proj
    if ($null -ne $app.Reattempt) {
        dotnet user-secrets set "youtube:Applications:$i`:Reattempt" "$($app.Reattempt)" --project $proj
    }
}
```

### Option C — convert to Azure app-setting names

```powershell
dotnet run --project Console-Apps/SecretsToFunctionSettings -- secrets/youtube-keys.local.json
```

## Step 4 — When bicep deploys work again

No manual step if Key Vault already has `Youtube-ApiKey-15` and `Youtube-ApiKey-16`. CI / `functions.bicep` provision will:

1. Read all `Youtube-ApiKey-*` secrets via `functions.bicepparam`
2. Wire slot 13 → `youTubeApiKey15`, slot 15 → `youTubeApiKey16`
3. Apply updated `CultPodcasts` DisplayNames

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
