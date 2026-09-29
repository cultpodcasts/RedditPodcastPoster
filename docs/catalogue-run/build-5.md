# Build 5 — Corpus migrate tooling

**Status:** open — [gate-4.md](./gate-4.md) is done. Apply still runs in GATE 5 only.

## Done when

Migrator tooling (+ any bridge-drop code) is **merged to `main`**. Apply runs in GATE 5.

## Checklist (when unlocked)

- [x] Candidate identify tools (`CatalogueMigrateIdentify` console + `FromEpisodes` — News allowlist for apply; stored URLs only; `--apply` refused)
- [~] Migrators News → Film → TV (keep GUIDs) — dry-run `CatalogueMigrate` plans only; `--apply` refused
- [~] Search swap helpers (`CatalogueMigrateSearchDocuments` + `PlayableSearchDocumentSwap`; transfer delegates; dry-run does not upload)
- [ ] Merged to `main`

## Notes

Dry-run default. `--apply` is GATE 5 and needs an explicit allowlist for News (S-008). Do not write Cosmos from this Build.

## When Build 5 is done

Open **only** [gate-5.md](./gate-5.md).
