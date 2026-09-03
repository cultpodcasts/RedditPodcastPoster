# PR conversation templates

Keep verbs **uppercase** on their own line so the other agent can parse them.

## Reviewer — new finding (inline)

```markdown
**P0|P1|P2** · **must-fix|nit**

**Problem:** <what is wrong, including smell / SOLID / missing test / devops miss>

**Solution:** <signpost the fix; point at existing types/files; do not dump a huge patch unless a tiny snippet is clearer>

Implementor: reply FIX, PARTIAL, WONTFIX, or NEED DIRECTION.
```

## Implementor — reply

```markdown
FIX
<one paragraph: what you will do / did>
```

```markdown
PARTIAL
<what you will change>
<what you will not, and why>
```

```markdown
WONTFIX
<reasoning; propose consensus or a follow-up issue>
```

```markdown
NEED DIRECTION
<the decision you need; 2–3 options if useful>
```

## Reviewer — after implementor

```markdown
OUTCOME: fixed | wontfix-accepted | partial-accepted | still-must-fix | deferred-to-issue <url>

<one paragraph>
```

Resolve the GitHub thread when `fixed` or `wontfix-accepted` and the PR is merge-safe. If `still-must-fix`, leave open. If no consensus, `deferred-to-issue` + kanban ticket; tell the parent the URL.

## Reviewer — cycle close (PR summary comment)

```markdown
## Review cycle <k> complete

- Acceptable for merging: **yes|no**
- Open threads: …
- New tickets: …
```
