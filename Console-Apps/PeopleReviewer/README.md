# PeopleReviewer

Local web UI to review and edit a **people-seed JSON** file before any Cosmos load.

**JSON on disk only.** This tool never writes Cosmos or episode documents.

## Run

```powershell
# Sample seed (bundled)
dotnet run --project Console-Apps/PeopleReviewer

# Your iteration seed (local / untracked)
dotnet run --project Console-Apps/PeopleReviewer -- `
  --seed-path "C:\path\to\people-seed.iteration-9.json" `
  --port 5188
```

Open **http://127.0.0.1:5188**

## Sort name UX

For each person:

| Field | Behaviour |
|-------|-----------|
| **Canonical name** | Editable; drives sort guess when sort name is not manually overridden |
| **Sort name** | Editable override; live **Sorts as:** hint below |
| **Organization / use full name for sorting** | Sets sort name to the full canonical name |
| Aliases / handles / notes | Same as the old PeopleMigrator reviewer |

Guessing rules mirror the website (`person-sort.ts`):

- Org/show keywords (podcast, news, CNN, …) or long/ALL-CAPS names → **full name**
- Otherwise → **last whitespace token** (hyphenated tokens kept whole)
- Manual sort edits are **not** overwritten when the name changes
- On save, redundant last-token guesses are stored as `null`/`omitted`; org full-name and manual overrides are kept

## Seed file

`people-seed*.json` files are gitignored (large / local). Point `--seed-path` at your file.

If you still have `people-seed.iteration-9.json` from the old `PeopleMigrator` folder (Desktop backup, Recycle Bin, etc.), use that path. The seed was never committed to git, so it cannot be recovered with `git show`.

A tiny `sample-people-seed.json` is included for smoke-testing the UI.

## Stop script

When started via the helper below, PID is written under `.local/`:

```powershell
.\Console-Apps\PeopleReviewer\scripts\start-reviewer.ps1 -SeedPath "C:\path\to\people-seed.iteration-9.json"
.\.local\stop-people-reviewer.ps1
```

## Options

| Flag | Default | Description |
|------|---------|-------------|
| `--seed-path` | bundled sample | Seed JSON to load and save |
| `--port` | `5188` | HTTP port |
