## Summary

<!-- What changed and why? Focus on behaviour and intent, not file lists. -->

-

## Related

<!-- Every PR for a ticket MUST link it with a closing keyword so it shows under the
     issue's Development panel. Same repo: `Closes #N`. Other repo (e.g. an Api or
     website companion of an RPP ticket): `Closes cultpodcasts/RedditPodcastPoster#N`.
     Do not use `Refs` for the ticket itself. The ticket stays open until ALL of its
     PRs merge (issue-close sibling guard, cultpodcasts/RedditPodcastPoster#1423).
     List companion PRs and the merge order below. -->

- Closes #
- Companions:

## Config / secrets

<!-- Required when this PR adds or depends on new Function App settings, Key Vault
     secret names, or related Worker/Pages keys. List **names only** (never values).
     Tick every target app/env before production switchover. -->

- [ ] No new app settings / secret **names**
- [ ] **Or** new setting **names** (manual apply — GHA provision is not the prod path):
  - `indexer-infra`: `<!-- e.g. twitter__ShortUrlOnlyWhenShareImage -->`
  - `discover-infra`: `<!-- same names if in coreSettings / that app -->`
  - `api-infra`: `<!-- same names if in apiSettings / that app -->`
- [ ] Bicep updated (`Infrastructure/functions.bicep`) when the setting is durable infra
- [ ] Related Cloudflare Worker / Pages keys (if any): document in Api / website PR `## Config / secrets`

### Production switchover (before calling release done)

1. Open this PR and read **Config / secrets** above.
2. Confirm each named key exists on every listed Function App (portal / `az functionapp config appsettings list`).
3. Do **not** treat code deploy alone as config applied — scripts are code-only.
4. Only then flip feature gates / enable behaviour that depends on those keys.

## Notes

<!-- Optional: intentional differences, migration/rollout notes, or follow-up work. -->

## Test plan

GitHub Actions are inactive for now — verify changes locally before merge.

- [ ] `dotnet build --configuration Release`
- [ ] `dotnet test --configuration Release`
- [ ] <!-- Add manual verification steps for the change -->
