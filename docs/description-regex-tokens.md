# Description regex tokens

A podcast's `DescriptionRegex` extracts the useful part of an episode description: when it matches, the
`body` named group (`${body}`) replaces the description; when it does not match, the description is left
unchanged.

The pattern may contain these recognised tokens, which are resolved **per episode** when the regex is applied:

| Token             | Replaced with                       |
|-------------------|-------------------------------------|
| `{episode-title}` | the episode's (raw) title           |
| `{podcast-name}`  | the podcast's name                  |

## Example: strip a leading episode title

Some podcasts start every description with the episode title, sometimes followed directly by the text with no
separator, for example:

```n<Guest Name>: <Episode Subject> - <More Title>CONTENT ADVISORY: ...
```n
The website's **Episode title prefix removal** description-regex preset is:

```
^\s*{episode-title}\s*[-–—:|]?\s*(?<body>.*)$
```

It strips the title (and an optional `-`, `–`, `—`, `:` or `|` separator) from the start of the description and
keeps everything after it. It does not depend on any literal text after the title, and an episode whose
description does not start with its title is left unchanged.

## How tokens are resolved

All description regexes are built by `DescriptionRegexTokens.Create` (`RedditPodcastPoster.Models.Podcasts`).

- Only the exact, lower-case tokens are recognised. Any other `{...}` text, including regex quantifiers such as
  `{2}` or `{2,3}`, is left exactly as written.
- A token whose opening brace is escaped (`\{episode-title}`, i.e. an odd number of backslashes before `{`)
  is literal text, not a token, so a description that really contains `{episode-title}` can still be matched.
  `\\{episode-title}` (an escaped backslash) is a literal backslash followed by the token.
- The value is HTML-decoded and every character is `Regex.Escape`d, so `&`, `(`, `?`, `[`, `.` etc. in a title
  match literally and can never inject regex syntax or named groups.
- Whitespace in the value is trimmed, and each internal whitespace run matches any whitespace run (`\s+`) in
  the description (e.g. a line break where the title has a space).
- Apostrophes (`'` `’` `‘`), quotes (`"` `“` `”`), dashes (`-` `–` `—`) and `&` match their typographic and
  HTML-entity variants.
- The value is wrapped in a non-capturing group, so a quantifier after a token (`{episode-title}?`) applies to
  the whole value.
- A missing/blank value expands to `(?!)`, which never matches: the description is left unchanged and nothing
  throws.
- A pattern containing tokens is compiled with `Podcast.DescriptionFlags` (`IgnoreCase | Singleline`).

## Token-free patterns

Patterns without tokens are compiled exactly as written with the options each caller has always used, so
existing podcasts behave exactly as before. In particular the YouTube episode enricher has historically
compiled the description regex with **no** options (everything else uses `IgnoreCase | Singleline`); it keeps
doing so for token-free patterns. Token patterns always use `IgnoreCase | Singleline` everywhere.

## Match timeout

Every description regex is compiled with `DescriptionRegexTokens.MatchTimeout` (2 seconds). If a match times out
(catastrophic backtracking) it is treated as no match and the description is left unchanged, instead of the
exception failing indexing/publishing. This is the only behaviour change for token-free patterns and only affects
pathological patterns that previously could hang.

## Validation

The Api (`POST podcast`) validates a submitted `descriptionRegex` after expanding the tokens with sample values,
so a pattern containing tokens is accepted when the rest of it is a valid regex. An invalid pattern is rejected
with `400 Bad Request` and the body `{"error":"Invalid description-regex: <parser message>"}`
(`ApiErrorResponse`); the podcast is not saved. A blank value clears the regex.
