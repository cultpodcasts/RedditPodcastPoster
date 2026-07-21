# ADR 0001: Folder equals namespace

- **Status:** Accepted
- **Date:** 2026-07-21
- **Related:** PRs #896–#909

## Context

Several assemblies had most `.cs` files at the project root, so namespaces were flat and hard to navigate. Over a series of PRs we nested types into folders that match sub-namespaces (Models, Durable hosts, smaller class libraries).

We need a durable house rule so new code does not reintroduce top-heavy layouts, and so IDE/analyzers can flag drift.

## Decision

1. **Folder = namespace.** Under a project, the folder path relative to the project root must match the namespace under that project’s `RootNamespace` (default: assembly / project name). Example: `Episodes/Episode.cs` → `RedditPodcastPoster.Models.Episodes`.
2. **Do not change `RootNamespace`** to invent a shorter namespace tree unless there is an intentional exception (see below). Prefer nesting folders instead.
3. **DI registration** lives in `Extensions/` (typically `ServiceCollectionExtensions`), namespace `….Extensions`.
4. **Usings before `namespace`.** All `using` directives appear above the file-scoped (or block) namespace declaration; no blank lines between usings.
5. Prefer `using` directives over fully qualified type names; use aliases only for name collisions.
6. **Durable Functions:** nested namespaces for hosts are fine; keep orchestration/activity/trigger **class names** and `[Function]` / `[DurableTask]` **names** stable (App Insights and Durable identity).
7. **Cosmos entities:** do not rename CLR type names, `ModelType` enum values, or `[JsonPropertyName]` values solely for folder layout.

### Enforcement

- Roslyn **IDE0130** (`dotnet_style_namespace_match_folder`) is enabled as a **warning** in the repo `.editorconfig`.
- Fix incrementally; do not treat IDE0130 as a CI error until the solution stays clean under normal development.

### Intentional exceptions

| Case | Approach |
|------|----------|
| `Third-Party/**` | IDE0130 suppressed in `.editorconfig` |
| `RedditPodcastPoster.DependencyInjection.Abstractions` | Types intentionally live in `RedditPodcastPoster.DependencyInjection`; project sets `RootNamespace` to that value so folder=namespace still holds |
| Durable **function class names** | May differ from folder leaf names only where renaming would break Durable/App Insights; namespaces still match folders |

## Consequences

- New types should be added under a domain/technical folder with a matching namespace.
- Reviewers and agents can reject top-level dumps of many unrelated types.
- Occasional IDE0130 warnings guide cleanup; suppressions must be justified and documented here or beside the code.
