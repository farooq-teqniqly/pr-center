# queue-derivation Specification

## RENAMED Requirements

- FROM: `### Requirement: Update detection flags only other people's activity since the marker`
- TO: `### Requirement: Update detection flags only other people's activity since my last review`

## MODIFIED Requirements

### Requirement: Update detection flags only other people's activity since my last review

`UpdateDetector` SHALL take `PullRequestFacts`, `myLogin`, and the user's
last-review instant (`myLastReviewedAt`, `DateTimeOffset?`) and return whether
the pull request has an update. It SHALL return has-update when at least one
update-worthy event -- a commit, a **human** comment, or a **human** review --
has an author other than `myLogin` and a timestamp strictly after
`myLastReviewedAt`. A null `myLastReviewedAt` -- the user has never reviewed
this pull request -- SHALL yield no update: an unreviewed pull request is new,
not updated, and the update indicator is meaningful only relative to a review.
The user's own events SHALL never produce has-update, events at or before the
baseline SHALL never produce has-update, and comments or reviews whose `IsBot`
flag is set SHALL never produce has-update. Commits are never filtered by actor
type: a bot commit is a real diff and counts.

#### Scenario: Never reviewed yields no update

- **WHEN** `myLastReviewedAt` is null
- **THEN** the detector returns no update, regardless of other people's activity
  on the pull request

#### Scenario: Other person's commit after my last review is an update

- **WHEN** another user's commit landed strictly after `myLastReviewedAt`
- **THEN** the detector returns has-update

#### Scenario: Bot commit after my last review is an update

- **WHEN** a bot-authored commit landed strictly after `myLastReviewedAt`
- **THEN** the detector returns has-update

#### Scenario: Own activity after my last review is not an update

- **WHEN** the only events after `myLastReviewedAt` are authored by `myLogin`
- **THEN** the detector returns no update

#### Scenario: Bot comment or review after my last review is not an update

- **WHEN** the only events after `myLastReviewedAt` are comments or reviews with
  `IsBot` set
- **THEN** the detector returns no update

#### Scenario: Activity at or before my last review is not an update

- **WHEN** every other person's event has a timestamp at or before
  `myLastReviewedAt`
- **THEN** the detector returns no update

#### Scenario: Other person's comment or review after my last review is an update

- **WHEN** another human submitted a comment or a review strictly after
  `myLastReviewedAt`
- **THEN** the detector returns has-update

### Requirement: Queue item carries identity and the derived outputs

The derivation SHALL produce, for each shown pull request, a `QueueItem`
carrying:

- the pull request's identity (stable id, owner, repository, number, title,
  URL, and author login);
- the last update (author login and instant, for display);
- its membership state (`AwaitingFirstReview` or `AwaitingReReview`);
- its has-update flag;
- the user's engagement: when the user last reviewed (the greatest submitted
  timestamp among the user's reviews regardless of their state, null when the
  user has no review in the facts). This same instant is the update baseline
  handed to `UpdateDetector`, so the displayed last-reviewed instant and the
  update baseline are provably the same instant;
- the reviewer roster;
- the covering reviewers, with the already-covered indicator derived from
  that list.

To stay within the baseline parameter limit, these SHALL be grouped into
cohesive sub-records (identity, last update, engagement, roster, covered-by)
rather than a flat parameter list. Hidden pull requests SHALL NOT produce a
`QueueItem`. The derivation SHALL NOT sort or group the items.

#### Scenario: Shown pull request yields a queue item

- **WHEN** a pull request derives to a shown membership state
- **THEN** a `QueueItem` is produced carrying that state plus the has-update
  flag, the last-reviewed instant, the roster, and the covering reviewers

#### Scenario: Hidden pull request yields no queue item

- **WHEN** a pull request derives to any hidden result (draft, closed,
  approved, or untracked)
- **THEN** no `QueueItem` is produced for it

#### Scenario: Never reviewed is explicit

- **WHEN** the user has no submitted review in the pull request's facts
- **THEN** the queue item's last-reviewed instant is null (rendered as "never"
  by the UI, not as a zero timestamp)

#### Scenario: Last reviewed reflects the user's latest review

- **WHEN** the user has submitted reviews on the pull request
- **THEN** the queue item's last-reviewed instant is the greatest submitted
  timestamp among them, whatever their states
