# Discovery scorer backfill evidence

Generated: 2026-06-13T17:29:17.7112357Z
Mode: live backfill
Auto-hide threshold: 0.05

## Training baseline
- Trained at: 2026-06-13T03:00:24.7933840Z
- Training rows: 156,351
- Test precision @ threshold 0.05: 52.04%
- Test recall @ threshold 0.05: 97.10%
- Interpretation: at this threshold the model hides ~97% of training rejects while ~52% of hidden items were true rejects (precision on predicted negatives / hidden bucket).

## Documents

| Document id | discoveryBegan (UTC) | state | result count |
|-------------|------------------------|-------|--------------|
| `ed3d2e2a-652d-4ea6-ae62-b535636ad9cb` | 2026-06-13T14:30:10.5197951Z | Unprocessed | 2 |

## Per-document summary

### `ed3d2e2a-652d-4ea6-ae62-b535636ad9cb` (2026-06-13T14:30:10.5197951Z)
- Total results: 2
- Auto-hidden: 0
- Visible: 2
- % hidden: 0.0%

## acceptProbability distribution (all documents)

| Bucket | Count | % of total |
|--------|------:|-----------:|
| 0 – 0.05 | 0 | 0.0% |
| 0.05 – 0.2 | 0 | 0.0% |
| 0.2 – 0.5 | 1 | 50.0% |
| 0.5+ | 1 | 50.0% |

## Top 10 highest acceptProbability

| showName | episodeName | acceptProbability | matchingPodcast |
|----------|-------------|------------------:|-----------------|
| THE ANTI AA CONCEPT | AA Exposed \| The Truth Behind Bill Wilson \| The Man Behind the AA Cult Allegations | 0.7321 | yes |
| The Vault: The Epstein Files | Mega Edition:  Jeffrey Epstein's Inner Circle And The Compensation Fund Controversy (6/13/26) | 0.3914 | yes |

## Sample auto-hidden results (up to 10)

| showName | episodeName | acceptProbability | matchingPodcast |
|----------|-------------|------------------:|-----------------|

## matchingPodcastIds signal (visible vs hidden)

| Visibility | Total | With matchingPodcast | Without | % with matching | Avg acceptProbability |
|------------|------:|---------------------:|--------:|----------------:|----------------------:|
| Visible | 2 | 2 | 0 | 100.0% | 0.5617 |
| Hidden | 0 | 0 | 0 | 0.0% | 0.0000 |

Training context: accepted discovery results disproportionately have matchingPodcastIds; the scorer should hide few rows with matches and many without.

