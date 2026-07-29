## MODIFIED Requirements

### Requirement: Queue item carries identity and the derived outputs

The derivation SHALL produce, for each shown pull request, a `QueueItem`
carrying:

- the pull request's identity (stable id, owner, repository, number, title,
  URL, and author login);
- the last update (author login and instant, for display);
- its derived status: membership state (`AwaitingFirstReview` or
  `AwaitingReReview`), its has-update flag, and its authored-by-me flag (true
  when the pull request's author login is the user's login, compared the same
  way the reviewer roster marks the user's own chip);
- the user's engagement: when the user last reviewed (the greatest submitted
  timestamp among the user's reviews regardless of their state, null when the
  user has no review in the facts). This same instant is the update baseline
  handed to `UpdateDetector`, so the displayed last-reviewed instant and the
  update baseline are provably the same instant;
- the reviewer roster;
- the covering reviewers, with the already-covered indicator derived from
  that list.

The authored-by-me flag is a display projection only. It SHALL NOT affect
membership: a pull request the user authored appears or is hidden by exactly the
same rules as any other pull request.

To stay within the baseline parameter limit, these SHALL be grouped into
cohesive sub-records (identity, last update, derived status, engagement, roster,
covered-by) rather than a flat parameter list. Hidden pull requests SHALL NOT
produce a `QueueItem`. The derivation SHALL NOT sort or group the items.

#### Scenario: Shown pull request yields a queue item

- **WHEN** a pull request derives to a shown membership state
- **THEN** a `QueueItem` is produced carrying that state plus the has-update
  flag, the authored-by-me flag, the last-reviewed instant, the roster, and the
  covering reviewers

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

#### Scenario: Authored-by-me flag is set for the user's own pull request

- **WHEN** a shown pull request's author login is the user's login
- **THEN** the queue item's authored-by-me flag is true

#### Scenario: Authored-by-me flag is clear for another author's pull request

- **WHEN** a shown pull request's author login is not the user's login
- **THEN** the queue item's authored-by-me flag is false
