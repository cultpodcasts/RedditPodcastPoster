# Short-URL-only social posts (share image)

When shortener KV metadata includes a share image, X/Bluesky can post **only**
the `s.cultpodcasts.com` short URL (no YouTube/Spotify/Apple link). Bluesky still
keeps platform `UrlService` for thumb fetch.

## Config gates (default OFF)

| Setting | Section | Default |
|---------|---------|---------|
| `ShortUrlOnlyWhenShareImage` | `twitter` | **`false`** |
| `ShortUrlOnlyWhenShareImage` | `bluesky` | **`false`** |

Bicep sets both to `'false'`. Shortener KV may still store image metadata when
writing keys; only the **post body** behaviour is gated.

## Enable later

1. Confirm website `FeatureSwitch.episodeOgShareImage` ON path on preview
   (see website `docs/episode-og-share-image.md`).
2. Set `twitter__ShortUrlOnlyWhenShareImage` / `bluesky__ShortUrlOnlyWhenShareImage`
   to `true` in app settings (or bicep) for indexer/api.
3. Dry-run / staging post for an episode with artwork → short URL only.
4. Episode without art → platform URL unchanged.

## Related

- Api page-details create-on-miss + `/og-image` (Api PR share-image work)
- Website SEO FeatureSwitch (default OFF)
