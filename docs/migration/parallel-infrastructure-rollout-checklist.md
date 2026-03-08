# Parallel Infrastructure Rollout Checklist

## Goal
Deploy a parallel Azure environment from this branch, migrate data to new Cosmos containers, validate behavior, cut over traffic, then retire legacy infrastructure.

## 1. Update deployment workflow (`.github/workflows/deploy.yml`)

- Add branch-aware environment variables for parallel deployment (resource group name, suffix, app names).
- Keep `main` behavior unchanged; branch deployment should target new resource names.
- Ensure Bicep deployment steps pass parameters for parallel naming and Cosmos settings.
- Add explicit guard so branch deploy does not overwrite production resource names.

## 2. Update Bicep templates (`Infrastructure/*.bicep`)

### `functions.bicep`
- Add parameters for:
  - parallel resource group naming/suffix
  - Cosmos database/container names for migration split
- Add app settings for migration-compatible configuration:
  - `cosmosdb__Container` (legacy source container)
  - `cosmosdb__PodcastsContainer` (new podcasts target)
  - `cosmosdb__EpisodesContainer` (new episodes target)
- Ensure app names and dependent resources are suffix-driven so they can run in parallel.

### New/updated Cosmos infra resources
- Define Cosmos account in parallel environment if isolating data-plane from production.
- Define database and containers:
  - `Podcasts`
  - `Episodes`
  - `subjects`
  - `activities`
  - `discovery`
  - `lookup` (contains the single `KnownTerms` item with `KnownTerms` type)

## 3. Update parameter file (`Infrastructure/functions.bicepparam`)

- Add secrets/params required by new Cosmos account (if separate account is created).
- Add values for new container settings (`PodcastsContainer`, `EpisodesContainer`).
- Keep existing legacy `Container` value for source reads during migration.

## 4. Deploy parallel infrastructure

- Trigger deployment from this feature branch.
- Validate:
  - New resource group created.
  - New function apps deployed with parallel names.
  - New Cosmos DB account/database/containers created.
  - App settings in deployed functions reflect split container config.

## 5. Run migration console app

- Run `Console-Apps/LegacyPodcastToV2Migration` against parallel environment settings.
- Record migration summary:
  - podcasts migrated
  - episodes migrated
  - failed podcast IDs
  - failed episode IDs
- Re-run migration after fixes until failures are resolved or accepted.

## 6. Continue feature implementation and validation

- Continue migration-plan code changes on branch.
- Validate API/indexer/discovery flows against parallel infra.
- Validate search behavior and compact search contract compatibility.
- Validate data parity between legacy source and new target containers.

## 7. Cutover

- Point URLs/endpoints to new infrastructure.
- Monitor logs, errors, throughput, and latency during stabilization window.
- Keep rollback path available to legacy environment during burn-in.

## 8. Merge and retire

- Merge to `main` after stabilization and sign-off.
- Take final backup/export of legacy data.
- Decommission old infra and old Cosmos account only after confirmed steady-state.
