# queue-derivation

Bot/CI policy amendments per the decision recorded 2026-07-10 in the idea and
state docs: bot comments and reviews are noise, bot commits are real diffs.
Dismissed reviews never reach the derivers (the adapter filters them), so no
dismissed clause appears here.

## MODIFIED Requirements

### Requirement: Update detection flags only other people's activity since the marker

`UpdateDetector` SHALL take `PullRequestFacts`, `myLogin`, and a last-seen
marker (`DateTimeOffset?`) and return whether the pull request has an update. It
SHALL return has-update when at least one update-worthy event -- a commit,
a **human** comment, or a **human** review -- has an author other than
`myLogin` and a timestamp strictly after the marker. A null marker SHALL be
treated as has-update. The user's own events SHALL never produce has-update,
events at or before the marker SHALL never produce has-update, and comments or
reviews whose `IsBot` flag is set SHALL never produce has-update. Commits are
never filtered by actor type: a bot commit is a real diff and counts.

#### Scenario: Unseen when never looked at

- **WHEN** the marker is null
- **THEN** the detector returns has-update

#### Scenario: Other person's commit after the marker is an update

- **WHEN** another user's commit landed after the marker instant
- **THEN** the detector returns has-update

#### Scenario: Bot commit after the marker is an update

- **WHEN** a bot-authored commit landed after the marker instant
- **THEN** the detector returns has-update

#### Scenario: Own activity after the marker is not an update

- **WHEN** the only events after the marker are authored by `myLogin`
- **THEN** the detector returns no update

#### Scenario: Bot comment or review after the marker is not an update

- **WHEN** the only events after the marker are comments or reviews with
  `IsBot` set
- **THEN** the detector returns no update

#### Scenario: Activity at or before the marker is not an update

- **WHEN** every other person's event has a timestamp at or before the marker
- **THEN** the detector returns no update

#### Scenario: Other person's comment or review after the marker is an update

- **WHEN** another human submitted a comment or a review after the marker
  instant
- **THEN** the detector returns has-update

### Requirement: Already-covered flag reflects other reviewers' submitted reviews

`CoveredFlag` SHALL return true when at least one submitted **human** review
(`IsBot` false) has a reviewer other than `myLogin`, and false otherwise. A
pending review *request* (no submitted review) SHALL NOT make a pull request
covered, and a bot review SHALL NOT make a pull request covered -- a bot
review is not human coverage.

#### Scenario: Another reviewer's review marks it covered

- **WHEN** another human has submitted any review (approved,
  changes-requested, or commented)
- **THEN** the flag is true

#### Scenario: Only pending requests does not mark it covered

- **WHEN** other reviewers are requested but none has submitted a review
- **THEN** the flag is false

#### Scenario: Only the user's own review does not mark it covered

- **WHEN** the only submitted reviews are authored by `myLogin`
- **THEN** the flag is false

#### Scenario: Only bot reviews does not mark it covered

- **WHEN** the only submitted reviews by others have `IsBot` set
- **THEN** the flag is false
