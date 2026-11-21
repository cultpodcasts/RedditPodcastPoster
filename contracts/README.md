# @cultpodcasts/contracts <!-- pragma: allowlist secret -->

TypeScript types, Zod schemas and JSON Schema generated from the Cult Podcasts Azure Functions DTOs. The generator lives in RedditPodcastPoster (`Tools/ContractExporter`); this folder is the published package. <!-- pragma: allowlist secret -->

Shapes match the isolated-worker HTTP serializer: `[JsonPropertyName]`, UTC instants ending in `Z`, `TimeSpan` and `Guid` as strings, and string enum names where the property uses `JsonStringEnumConverter`. A field is optional only when the serializer omits null.

```ts
import type { EpisodeDto, EpisodeUpdateResponse, SearchIndexerState } from "@cultpodcasts/contracts"; <!-- pragma: allowlist secret -->
import { episodeDtoSchema } from "@cultpodcasts/contracts/zod"; <!-- pragma: allowlist secret -->
```

JSON Schema bundle: `@cultpodcasts/contracts/schemas` (`#/$defs/EpisodeDto` and the other definitions). <!-- pragma: allowlist secret -->

Zod is a peer dependency (`zod@^3.25.0 || ^4.0.0`).

## Install

The package is on GitHub Packages under the `cultpodcasts` user (not an organisation). <!-- pragma: allowlist secret -->

```ini
@cultpodcasts:registry=https://npm.pkg.github.com <!-- pragma: allowlist secret -->
//npm.pkg.github.com/:_authToken=${NODE_AUTH_TOKEN}
```

`NODE_AUTH_TOKEN` is a GitHub token with `read:packages`. Do not commit it.

- **Api and website CI** cannot use that repo's `GITHUB_TOKEN` to read a package published from RedditPodcastPoster. Use a PAT.
- **Cloudflare Workers Builds** need the same PAT as a build variable named `NODE_AUTH_TOKEN`, and the `.npmrc` lines above in the project that runs `npm ci`. It is a build secret, not a Worker secret.

Maintainer notes, including how to add a DTO: `docs/typescript-contracts.md` in RedditPodcastPoster.
