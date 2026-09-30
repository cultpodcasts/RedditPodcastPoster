# Build 5 — Corpus migrate tooling

**Status:** done — merged to `main` as #1001 (`7641389f`).

## Done when

Migrator tooling is **merged to `main`**. Apply is not this Build.

## Checklist

- [x] Candidate identify tools (`CatalogueMigrateIdentify`)
- [x] Migrators News → Film → TV dry-run plans (`CatalogueMigrate`; keep GUIDs)
- [x] Search swap helpers (dry-run does not upload)
- [x] Merged to `main` (#1001)

## Notes

Dry-run default. **`--apply` always exits 2** in this checkout (writes not implemented). GATE 5 unlock is not apply. Agents must not pass `--apply`.

## When Build 5 is done

Open **only** [gate-5.md](./gate-5.md).
