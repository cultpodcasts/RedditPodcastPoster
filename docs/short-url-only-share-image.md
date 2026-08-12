# Short-URL-only social posts (share image)

When shortener KV metadata includes a share image, X/Bluesky can post **only**
the `s.cultpodcasts.com` short URL (no YouTube/Spotify/Apple link). Bluesky still
keeps platform `UrlService` for thumb fetch.

## Config gates (default OFF)

| Setting | Section | Default |
|---------|---------|---------|
| `ShortUrlOnlyWhenShareImage` | `twitter` | **`false`** |
| `ShortUrlOnlyWhenShareImage` | `bluesky` | **`false`** |

Bicep (`Infrastructure/functions.bicep`) sets both app settings to `'false'`.
Shortener KV may still store image metadata when writing keys; only the **post
body** behaviour is gated.

### Manual deploy (required)

GitHub Actions is **not** the working provision path. After merge, apply the
app-setting **names** below manually (portal / `az functionapp config appsettings
set` / non-GHA infra) — code deploy alone does not add them:

- `twitter__ShortUrlOnlyWhenShareImage`
- `bluesky__ShortUrlOnlyWhenShareImage`

## Enable later

1. Confirm website `FeatureSwitch.episodeOgShareImage` ON path on preview
   (see website `docs/episode-og-share-image.md`).
2. Manually set `twitter__ShortUrlOnlyWhenShareImage` /
   `bluesky__ShortUrlOnlyWhenShareImage` to `true` in app settings for
   indexer (and any other app that binds those options).
3. Dry-run / staging post for an episode with artwork → short URL only.
4. Episode without art → platform URL unchanged.

## Related

- Api page-details create-on-miss + `/og-image` (Api PR share-image work)
- Website SEO FeatureSwitch (default OFF)
