# Indexer YouTube go-live checklist

Production checklist for deploying the **indexer** (`indexer-infra`) with the flat YouTube key ring, quota reporting, and cultpodcasts keys on slots 13/15.

**Related docs:**

- [youtube-keys.md](youtube-keys.md) — key vault naming, slot map, literal app settings
- [indexing-app-insights-queries.md](indexing-app-insights-queries.md) — KQL validation at 06:00 / 18:00 UTC
- [interim-deployment.md](interim-deployment.md) — local deploy when GitHub Actions is offline

---

## Architecture (reminder)

```
Pacific quota day
    │
    ▼
Single flat indexer key ring (slots 1–4, 8–11, 13–16 — config order, deduped)
    │  persisted ring position; hour spread fallback when no state
    ▼
YouTube enabled when hour % 6 == 0  →  00, 06, 12, 18 UTC
    │
    ├─ Passes 1–2 at 00 / 12 UTC
    └─ Passes 3–4 at 06 / 18 UTC
```

- **Ring exhaustion:** if all keys are exhausted, later passes may run with `skip-youtube='True'`.
- **Runtime config:** app reads **literal** `youtube__Applications__*` strings from app settings only — never Key Vault.

---

## 1. Prerequisites

- [ ] `az login` and subscription **Cultpodcasts**
- [ ] Google Cloud API keys for indexer ring slots **13** and **15** (DisplayNames `Indexer-Key-09`, `Indexer-Key-11`)
- [ ] Key Vault `cultpodcasts-deployment` has `Youtube-ApiKey-15` and `Youtube-ApiKey-16` (deploy-time source)
- [ ] Literal key strings ready for app settings
- [ ] Target: **`indexer-infra`** (RG `AutomatedInfra`)

**Do not** set `@Microsoft.KeyVault(...)` reference URIs on YouTube app settings.

---

## 2. App settings

### Sync DisplayNames on all three function apps

```powershell
.\scripts\apply-youtube-keys.ps1 -DisplayNamesOnly
```

Indexer DisplayNames are `Indexer-Key-01-CultPodcasts` through `Indexer-Key-12-CultPodcasts` (flat ring — no Reattempt field).

### Apply keys (if needed)

```powershell
.\scripts\apply-youtube-keys.ps1 -FromKeyVault
```

Or manual slot 13/15 update — see [youtube-keys.md § Step 2](youtube-keys.md#step-2--apply-to-live-function-apps-bicep-deploy-offline).

Checklist:

- [ ] Slots 13 and 15 `ApiKey` are **literal** `AIza...` strings
- [ ] Slots 13 and 15 `Name` = `cultpodcasts` (lowercase)
- [ ] All indexer slots use `Indexer-Key-NN-CultPodcasts` DisplayNames
- [ ] App restarted after settings change

---

## 3. Deploy indexer code

```powershell
.\scripts\deploy-indexer.ps1
```

Deploy scripts do **not** change app settings.

---

## 4. Verify — 06:00 or 18:00 UTC window

See [indexing-app-insights-queries.md](indexing-app-insights-queries.md) for full KQL.

| Step | What to confirm |
|------|-----------------|
| Timer fired | `RunHourly initiated hour-utc='6'` or `'18'` |
| Pass selection | `youtube-enabled-hour='True'` on YouTube hours |
| Key ring | No sustained `ring exhausted`; rotation only if quota hit |
| Batch 4 | `skip-youtube='False'`, `success='True'` |

---

## 5. Quota report — 06:55 UTC

Daily `YouTubeQuotaReport` timer flushes prior Pacific quota day to Cosmos `LookUps`.

```powershell
$yesterday = (Get-Date).ToUniversalTime().AddDays(-1).ToString("yyyy-MM-dd")
.\scripts\query-cosmos-lookups.ps1 -Query QuotaReport -ReportDate $yesterday
```

---

## 6. Order of operations

1. Prerequisites — login, keys in hand
2. **App settings** — DisplayNames (`-DisplayNamesOnly`) and keys if needed
3. **Deploy indexer code**
4. Wait for YouTube window (06:00 or 18:00 UTC)
5. Validate indexing (§4)
6. Next 06:55 UTC — confirm quota report in Cosmos

---

## 7. Rollback

| Layer | Rollback |
|-------|----------|
| **Code** | Redeploy previous package via `deploy-indexer.ps1` |
| **App settings** | Restore prior ApiKey / DisplayName values via Portal or `az` |
| **Cosmos state** | `YouTubeIndexerKeyState` is forward-compatible; delete only for intentional ring reset |

---

## Future — when bicep provision works

Ensure KV has all secrets; run `functions.bicep` provision. Bicep writes literal keys and flat `Indexer-Key-NN` DisplayNames automatically.
