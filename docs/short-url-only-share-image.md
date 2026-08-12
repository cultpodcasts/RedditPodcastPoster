# Short-URL-only social posts (share image)

When shortener KV metadata includes a share image, X/Bluesky can post **only**
the `s.cultpodcasts.com` short URL (no YouTube/Spotify/Apple link). Bluesky still
keeps platform `UrlService` for thumb fetch.

## Config gates (default OFF)

| Setting | Section | App setting **name** | Default |
|---------|---------|----------------------|---------|
| `ShortUrlOnlyWhenShareImage` | `twitter` | `twitter__ShortUrlOnlyWhenShareImage` | **`false`** |
| `ShortUrlOnlyWhenShareImage` | `bluesky` | `bluesky__ShortUrlOnlyWhenShareImage` | **`false`** |

Bicep (`Infrastructure/functions.bicep`) puts both keys on **coreSettings**, so they
apply to **`indexer-infra`**, **`api-infra`**, and **`discover-infra`**.

Shortener KV may still store image metadata when writing keys; only the **post
body** behaviour is gated.

### Manual deploy (required)

GitHub Actions is **not** the working provision path. After merge / before
production switchover, confirm the app-setting **names** below on each Function
App (portal / `az functionapp config appsettings list`) — code deploy alone does
not add them:

- `twitter__ShortUrlOnlyWhenShareImage`
- `bluesky__ShortUrlOnlyWhenShareImage`

Targets: `indexer-infra`, `api-infra`, `discover-infra` (all via coreSettings).

PR bodies **must** list these names under `## Config / secrets` (never values).

## Production switchover checklist

Before calling a production release done (or flipping the gates ON):

1. [ ] RPP PR `## Config / secrets` lists both setting **names** and the three apps
2. [ ] Keys present on `indexer-infra`, `api-infra`, `discover-infra` (verify with `az`)
3. [ ] Website SEO path validated on preview (`FeatureSwitch.episodeOgShareImage`)
4. [ ] Leave both settings **`false`** for initial code deploy (platform URLs unchanged)
5. [ ] Only later: set both to `true` when intentionally enabling short-URL-only posts

## Enable later

1. Confirm website `FeatureSwitch.episodeOgShareImage` ON path on preview
   (see website `docs/episode-og-share-image.md`).
2. Manually set `twitter__ShortUrlOnlyWhenShareImage` /
   `bluesky__ShortUrlOnlyWhenShareImage` to `true` on the Function Apps above.
3. Dry-run / staging post for an episode with artwork → short URL only.
4. Episode without art → platform URL unchanged.

## Related

- Api page-details create-on-miss + `/og-image` (Api PR share-image work)
- Website SEO FeatureSwitch (default OFF)
