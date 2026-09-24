# Episode wire rewrite (throwaway)

**Delete this whole `films-refactor/` folder after GATE 1 succeeds.**  
Not in `RedditPodcastPoster.slnx`. Not published by `publish-console-apps.ps1`.

Requirements: [docs/catalogue-run/build-1-rewrite-console-requirements.md](../docs/catalogue-run/build-1-rewrite-console-requirements.md)  
**If migrate + rollback both fail:** [REPAIR.md](./REPAIR.md)

## Build / test

```powershell
dotnet test films-refactor/EpisodeWireRewrite.Tests/EpisodeWireRewrite.Tests.csproj -c Release
```

## Run (GATE 1 only)

Dry-run (default — no Cosmos writes). **Repair evidence is always written** under `./wire-cutover-evidence/`:

```powershell
dotnet run --project films-refactor/EpisodeWireRewrite -- --all
```

Small live batch (first 10 needing cutover, stable `ORDER BY c.id`; already-migrated = no-action log, not failure):

```powershell
dotnet run --project films-refactor/EpisodeWireRewrite -- --limit 10 --apply
```

Full apply:

```powershell
dotnet run --project films-refactor/EpisodeWireRewrite -- --all --apply
```

Optional: `--journal path\to\run.jsonl` only overrides where the pack is written.

Rollback (must point at the evidence pack from migrate):

```powershell
dotnet run --project films-refactor/EpisodeWireRewrite -- --rollback --journal .\wire-cutover-evidence\wire-cutover-….jsonl --apply
```

## Evidence files (always on migrate — not opt-in)

| File | Contents |
|------|----------|
| `*.jsonl` | Per episode: ids, `applied`, full `before`/`after`, `fieldChanges` from→to |
| `*-affected-ids.txt` | Every planned/touched episode |
| `*-applied-ids.txt` | Only rows Cosmos patched successfully |
| `*-changes.txt` | Human `field: from -> to` lines |

Progress lines print continuously (scanned / needChange / written / rate / last id).
