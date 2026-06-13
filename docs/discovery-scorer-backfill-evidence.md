# Discovery scorer backfill evidence

Generated: 2026-06-13T11:58:59.4546049Z
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
| `1eddd76d-fa93-474a-8025-219fb9624b88` | 2026-06-13T02:30:08.0559350Z | Unprocessed | 86 |
| `de7be51c-84ac-4251-b8b1-d23592ef4452` | 2026-06-13T08:30:14.5316414Z | Unprocessed | 58 |

## Per-document summary

### `1eddd76d-fa93-474a-8025-219fb9624b88` (2026-06-13T02:30:08.0559350Z)
- Total results: 86
- Auto-hidden: 79
- Visible: 7
- % hidden: 91.9%

### `de7be51c-84ac-4251-b8b1-d23592ef4452` (2026-06-13T08:30:14.5316414Z)
- Total results: 58
- Auto-hidden: 52
- Visible: 6
- % hidden: 89.7%

## acceptProbability distribution (all documents)

| Bucket | Count | % of total |
|--------|------:|-----------:|
| 0 – 0.05 | 131 | 91.0% |
| 0.05 – 0.2 | 0 | 0.0% |
| 0.2 – 0.5 | 3 | 2.1% |
| 0.5+ | 10 | 6.9% |

## Top 10 highest acceptProbability

| showName | episodeName | acceptProbability | matchingPodcast |
|----------|-------------|------------------:|-----------------|
| Echoes and Oddities | Bleached Hair and Stolen Lives \| The Family Part 2 | 0.9809 | yes |
| Surviving-ISH Podcast | From Pure Contempt to Empathy: How 'I've Had It' & 'A Necessary Conversation' Rewired My Brain | 0.9523 | yes |
| Crime Stories with Nancy Grace | PSYCHIC TIKTOK INFLUENCER APPEALS $10 MILLION VERDICT IN UNIVERSITY OF IDAHO SLAYINGS DEFAMATION CASE | 0.8482 | yes |
| True Weird Stuff | Revisiting Unholy City: The Cult Leader Who Built His Own Town | 0.8220 | yes |
| Hot On The Case | Doomsday Cult Couple Kills Their Families.. The Vallow-Daybell Cult Murders | 0.8045 | yes |
| By Kai Nicole | Inside the Wife School Cult – Raw Victim Interviews \| Part 6 | 0.7806 | yes |
| California News Today \| 2 Min News \| The Daily News Now! | Cult Leader Gets Life Sentence | 0.7565 | no |
| True Weird Stuff | Revisiting Unholy City: The Cult Leader Who Built His Own Town | 0.7555 | yes |
| California News Today \| 2 Min News \| The Daily News Now! | Cult Leader Sentenced to Life | 0.7147 | no |
| California News Today \| 2 Min News \| The Daily News Now! | Cult Leader Sentenced to Life | 0.7147 | no |

## Sample auto-hidden results (up to 10)

| showName | episodeName | acceptProbability | matchingPodcast |
|----------|-------------|------------------:|-----------------|
| Bloomberg Hot Pursuit! | Audi's New Supercar | 0.0000 | no |
| Sacrilegious Discourse - Bible Study for Atheists | Matthew Chapter 16: Bible Study by Atheists | 0.0000 | no |
| Too Hot For TV | S01 E09 - Troy L. Foreman Interview | 0.0000 | no |
| Истории У Дедушки | ЭТОТ ВЕЧЕР Я НЕ ЗАБУДУ НИКОГДА ДОЧЬ СКАЗАЛА МНЕ: "ПАПА НЕ ИЩИ МЕНЯ" | 0.0000 | no |
| Fashion Verdict with ZellSwag | EP 06: Is the Culture DEAD or Just OVERSATURATED \| Streetwear on Trial \| | 0.0000 | no |
| Gyanesh Sir's Classes : UPSC-CGPSC-GEOGRAPHY | UPSC - CGPSC \|\| Ancient, mediaval History & Art & Cult \|\| L-28 | 0.0000 | no |
| Fluent Fiction - Turkish | Conquering Heights: Emir’s Unexpected Adventure in İstanbul | 0.0000 | no |
| Jason Whitlock Clips | Stephanie White Surrenders to the Caitlin Clark GOAT Cult | 0.0000 | no |
| LucifersSeer | Lucifer's Legion - Apocalypse Death Cult | 0.0000 | no |
| Miaaaah!! | (Slowed) Guilty Pleasures - Peace Cult | 0.0000 | no |

## matchingPodcastIds signal (visible vs hidden)

| Visibility | Total | With matchingPodcast | Without | % with matching | Avg acceptProbability |
|------------|------:|---------------------:|--------:|----------------:|----------------------:|
| Visible | 13 | 10 | 3 | 76.9% | 0.7138 |
| Hidden | 131 | 5 | 126 | 3.8% | 0.0004 |

Training context: accepted discovery results disproportionately have matchingPodcastIds; the scorer should hide few rows with matches and many without.

