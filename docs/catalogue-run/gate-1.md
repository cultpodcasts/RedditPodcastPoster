# GATE 1 — Episode JSON cutover

**Status:** locked — open only after [build-1.md](./build-1.md) is done.

Build 1 done means: merged to `main` **and** rewrite console app is on `main`.  
If the rewrite tool is missing, **stop — GATE 1 is blocked** (you may not have opened this file yet; go back to Build 1).

Do not start Build 2 until this GATE is done.

---

## Done when

1. Functions + CLIs deployed with dual-key code.
2. Smoke checks pass.
3. Episode rewrite applied (after backup + dry-run + approval).
4. Audit count ≈ 0.
5. Search refreshed.
6. Dual-key **left on** (do not remove old-name support yet).

---

## Goal (this GATE only)

Deploy Build 1 safely against today’s Episode data, then rename Episode fields in Cosmos without losing episodes or bringing back removed shows.

---

## Steps (in order)

| # | Do this | Downtime? |
|---|---------|-----------|
| 0 | **Backup** all Episodes (keep until audit is green) | No |
| 1 | Create five empty Cosmos containers + set `cosmosdb__*` on Indexer, Discover, Api | No |
| 2 | **DEPLOY** Indexer → Discover → Api + publish CLIs | No |
| 3 | Smoke: homepage recent looks normal; removed shows stay hidden | No |
| 4 | **Run rewrite app dry-run** (`--all` or `--limit N`) — evidence always written; watch progress | No |
| 4b | Optional: **`--limit N --apply`** small live batch (stable id order; already-migrated = no-action) | Brief |
| 5 | Pause Indexer + Discover + Api writes | Hours |
| 6 | **Run rewrite app `--apply`** (explicit approval only) | During pause |
| 6b | If wrong: **`--rollback --journal <evidence.jsonl> --apply`** then stop | During pause |
| 7 | Resume apps; audit ≈ 0; smoke again | End pause |
| 8 | Refresh Search (keep reading both field names until sure) | Brief gap |
| 9 | Leave dual-key on — do not strip old-name support in code yet | No |

---

## DEPLOY nameplate (step 2)

| Deploy | Ship? |
|--------|-------|
| Indexer | Yes (first) |
| Discover | Yes (second) |
| Api | Yes (third) |
| CLIs | Yes (includes rewrite tool) |
| Search | Step 8 only — not in this wave |
| Worker / Site | No |

Scripts: [deployment.md](../deployment.md)

---

## Audit (step 7) — must be ≈ 0

```sql
SELECT COUNT(1) FROM c
WHERE c.type = 'Episode'
  AND (
    IS_DEFINED(c.podcastSearchTerms)
    OR IS_DEFINED(c.podcastLanguage)
    OR IS_DEFINED(c.podcastMetadataVersion)
    OR IS_DEFINED(c.podcastRemoved)
    OR NOT IS_DEFINED(c.releaseSort)
  )
```

---

## Safe holding points

- After step 3 (deploy + smoke): **safe to pause for days** with dual-key live.
- After step 9: GATE 1 done; dual-key still on.

---

## Do not do in this GATE

- Do not remove dual-key / bridges.
- Do not start Build 2 code assuming only new field names.
- Do not rewrite without backup and dry-run.
- Do not use a full-document Save that might strip unrelated leftover JSON — patch only the rename fields.

---

## When GATE 1 is done

Open **only** [build-2.md](./build-2.md).

---

## Dig deeper (still GATE 1 only)

| Topic | Where |
|-------|--------|
| Tool build requirements (already shipped in Build 1) | [build-1-rewrite-console-requirements.md](./build-1-rewrite-console-requirements.md) |
| **Repair evidence / fixer contract** | [films-refactor/REPAIR.md](../../films-refactor/REPAIR.md) |
| Field rename table | Below |
| Why day-to-day Save is not enough | Cold episodes never get rewritten without the tool |
| Deploy how-to | [deployment.md](../deployment.md) |

### Fields renamed (this GATE)

| Old | New |
|-----|-----|
| podcastSearchTerms | publisherSearchTerms |
| podcastLanguage | publisherLanguage |
| podcastMetadataVersion | parentMetadataVersion |
| podcastRemoved | parentRemoved |
| *(missing)* | releaseSort (copied from release date) |

Unchanged: `podcastId`, `podcastName`.
