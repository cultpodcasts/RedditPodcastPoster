# Build 1 blocker — Episode rewrite console requirements

**Parent:** [build-1.md](./build-1.md)  
**Used in:** [gate-1.md](./gate-1.md) steps 4 (dry-run) and 6 (`--apply`)  
**Not used in:** Build 1 itself (ship only)

---

## Purpose

One-shot tool to rewrite production Episode JSON:

| From | To |
|------|-----|
| `podcastSearchTerms` | `publisherSearchTerms` |
| `podcastLanguage` | `publisherLanguage` |
| `podcastMetadataVersion` | `parentMetadataVersion` |
| `podcastRemoved` | `parentRemoved` |
| *(absent)* | `releaseSort` (from existing `release` / release date) |

Leave unchanged: `podcastId`, `podcastName`, and **all other** Episode fields.

After GATE 1 succeeds, delete the whole folder from source.

---

## Layout (easy to remove)

| Rule | Detail |
|------|--------|
| **Folder** | Repo root `films-refactor/` (name signals throwaway catalogue cutover work) |
| **Not in** `RedditPodcastPoster.slnx` | Must **not** appear in the solution — CI `dotnet test` on the slnx must not require it |
| **Not in** `scripts/publish-console-apps.ps1` | Must not publish to `artifacts/tools/` |
| **May** `ProjectReference` | Existing Class-Libraries (Models, Persistence, Persistence.Abstractions, Configuration, etc.) |
| **Removal** | Delete `films-refactor/` when GATE 1 is done — no slnx / publish script edits needed |

Suggested shape:

```text
films-refactor/
  EpisodeWireRewrite/           # console
  EpisodeWireRewrite.Tests/     # business-rule + unit tests
  README.md                     # how to build, dry-run, --apply
```

Build / test without the solution:

```powershell
dotnet test films-refactor/EpisodeWireRewrite.Tests/EpisodeWireRewrite.Tests.csproj -c Release
dotnet run --project films-refactor/EpisodeWireRewrite -- --help
```

---

## CLI behaviour

| Requirement | Detail |
|-------------|--------|
| **Default = dry-run** | No Cosmos writes unless `--apply` |
| **`--apply`** | Explicit flag; refuse to write without it |
| **`--limit N`** | Work on at most N episodes that still need cutover (stable `ORDER BY c.id`). Already-migrated = no-action log, not failure. `0` = unlimited |
| **Repair evidence (always on)** | Every migrate writes JSONL + ids + from→to change log (default `./wire-cutover-evidence/`; `--journal` only overrides path) |
| **`--rollback`** | Restore from a prior evidence pack’s Before (pass `--journal` to select which pack) |
| **Progress (first-class)** | Continuous lines: scanned / needChange / written / failed / rate / last id / elapsed |
| **Report** | Final RESULT line + evidence paths |
| **Idempotent** | Second migrate after success: ~0 changes |
| **Fail loud** | Non-zero exit on partial failure |
| **No Function host** | Standalone console; same Cosmos settings pattern as other tools |

GATE 1 must pause Indexer + Discover + Api writes before `--apply`.

---

## Write safety (no data loss)

| Requirement | Detail |
|-------------|--------|
| **Surgical update** | Prefer Cosmos **patch** (or equivalent) that only sets/removes the cutover fields — **not** a full typed `Save()` document replace that can strip unmapped leftover JSON |
| **Remove legacy keys** | After copying values to new names, remove `podcastSearchTerms` / `podcastLanguage` / `podcastMetadataVersion` / `podcastRemoved` when present |
| **Set `releaseSort`** | From existing release instant when missing; do not invent dates |
| **Do not touch** | `podcastId`, `podcastName`, services, titles, guests, handles, language (episode), etc. |
| **Hidden parents** | Docs with `podcastRemoved: true` must become `parentRemoved: true` (and legacy key removed) — must stay hidden under dual-key queries |
| **Dry-run fidelity** | Dry-run counts must match what `--apply` would change (same selection rules) |

---

## Tests (mandatory)

Follow [`.cursor/rules/unit-tests.mdc`](../../.cursor/rules/unit-tests.mdc) and the always-on enforcement rule.

| Layer | Required |
|-------|----------|
| **BusinessRules** | Plain-English `DisplayName` rules for every cutover behaviour below |
| **Unit tests** | Processor / patch builder / CLI flag parsing (AutoMocker where mocks-heavy) |
| **Sections** | Every test: `// Arrange` / `// Act` / `// Assert` |
| **No brand names** | Scenario fixtures / specimens only |
| **Guardrails** | `pwsh ./scripts/assert-unit-test-guardrails.ps1` clean on changed test files |

### Business rules that must have tests

**Migrate**

1. Legacy-only doc → new names + `releaseSort`; legacy keys gone.  
2. Already-new doc → no change (idempotent).  
3. Mixed (some new, some legacy) → fill gaps; remove remaining legacy keys.  
4. `podcastRemoved: true` only → `parentRemoved: true`; legacy removed.  
5. Missing `releaseSort` but has `release` → `releaseSort` set from release.  
6. Already has `releaseSort` → leave value unchanged.  
7. Unrelated properties preserved.  
8. `podcastId` / `podcastName` / `id` never renamed or removed.

**Rollback (more tests than migrate — rollback is the safety net)**

9. Rollback restores legacy keys from Before exactly.  
10. `parentRemoved` → back to `podcastRemoved` when that was Before.  
11. Added `releaseSort` removed on rollback when Before lacked it.  
12. Pre-existing `releaseSort` raw value restored.  
13. Unrelated/identity fields untouched.  
14. RollbackTarget === Before (no re-derive).  
15. Patch delta After→Before non-empty after migrate; empty when already Before.  
16. Remigrate after rollback matches first After.  
17. Journal preserves episode/podcast ids.

**Progress / journal / processor**

18. Progress reporter emits during run.  
19. Dry-run journals affected ids with zero writes.  
20. Rollback processor restores store from journal Before.

Put rules under `films-refactor/EpisodeWireRewrite.Tests/BusinessRules/`.

---

## Done (clears Build 1 tool blocker)

- [x] `films-refactor/` exists; **not** in `.slnx`; **not** in publish-console list  
- [x] Dry-run default + `--apply` + `--rollback`; repair evidence **always** written on migrate  
- [x] Surgical field updates only  
- [x] Continuous progress reporting  
- [x] Affected/applied ids + from→to change log (journal pack)  
- [x] BusinessRules + processor tests green via `dotnet test` on the test csproj  
- [x] Short `films-refactor/README.md` + `REPAIR.md`  
- [ ] Merged to `main`  

Then open [gate-1.md](./gate-1.md) — that is when you **run** the tool.
