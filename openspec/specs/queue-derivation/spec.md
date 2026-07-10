# queue-derivation Specification

## Purpose
All derivation is evaluated relative to the user, identified by a login passed
in as a plain value (`myLogin`). The derivers are pure: same inputs, same
outputs, no I/O. See [pr-center-state.md](../../../docs/pr-center-state.md) for
the machines these encode.

## Requirements

### Requirement: Membership is derived per poll from current facts

`MembershipDeriver` SHALL compute a pull request's membership as a pure function
of `PullRequestFacts` and `myLogin`, with no stored transition history. A draft
pull request SHALL be excluded even when the user is a requested reviewer. A
closed or merged pull request SHALL be dropped. Otherwise membership SHALL
follow the latest-review-verdict rule: let `amRequested` be whether `myLogin`
is a directly requested reviewer, and `myLatest` be the user's review with the
greatest submitted timestamp. When two of the user's reviews share the greatest
timestamp (GitHub review timestamps are second-granularity), the tie SHALL be
broken toward the most actionable verdict -- commented, then changes-requested,
then approved -- so a tie keeps the pull request shown rather than dropping it.
This tie-break SHALL apply only to equal timestamps and SHALL NOT override a
strictly-later review.

- `amRequested` and `myLatest` is null: **AwaitingFirstReview**.
- `amRequested` and `myLatest` is approved: **AwaitingFirstReview**.
- `myLatest` is commented or changes-requested (regardless of `amRequested`):
  **AwaitingReReview**.
- not `amRequested` and `myLatest` is null: **hidden (untracked)**.
- not `amRequested` and `myLatest` is approved: **hidden (approved)**.

Only `AwaitingFirstReview` and `AwaitingReReview` are shown states.

#### Scenario: Draft is excluded even when requested

- **WHEN** the user is a requested reviewer on a draft pull request
- **THEN** the deriver returns a hidden (excluded: draft) result, not a shown
  state

#### Scenario: Closed or merged is dropped

- **WHEN** a pull request is closed or merged
- **THEN** the deriver returns a hidden (dropped) result regardless of review
  state

#### Scenario: Requested with no prior review awaits first review

- **WHEN** the user is a requested reviewer and has submitted no review
- **THEN** the deriver returns AwaitingFirstReview

#### Scenario: A non-approved review awaits re-review

- **WHEN** the user's latest review is commented or changes-requested and the
  pull request is open and not draft
- **THEN** the deriver returns AwaitingReReview, whether or not the user is
  currently a requested reviewer

#### Scenario: Approval drops the pull request

- **WHEN** the user's latest review is approving and the user is not currently
  a requested reviewer
- **THEN** the deriver returns a hidden (approved) result

#### Scenario: Re-request after approval awaits first review again

- **WHEN** the user's latest review is approving and the user is again a
  directly requested reviewer
- **THEN** the deriver returns AwaitingFirstReview

#### Scenario: Never requested and never reviewed is untracked

- **WHEN** the user is not a requested reviewer and has no review on the pull
  request
- **THEN** the deriver returns a hidden (untracked) result

#### Scenario: Same-timestamp tie keeps the pull request shown

- **WHEN** the user has an approving review and a non-approving review with the
  same submitted timestamp, and is not a requested reviewer
- **THEN** the deriver returns AwaitingReReview, because the tie breaks toward
  the non-approving verdict

#### Scenario: A strictly-later approval still drops the pull request

- **WHEN** the user's approving review has a later submitted timestamp than a
  prior non-approving review, and is not a requested reviewer
- **THEN** the deriver returns a hidden (approved) result

### Requirement: Update detection flags only other people's activity since the marker

`UpdateDetector` SHALL take `PullRequestFacts`, `myLogin`, and a last-seen
marker (`DateTimeOffset?`) and return whether the pull request has an update. It
SHALL return has-update when at least one update-worthy event -- a commit,
comment, or review -- has an author other than `myLogin` and a timestamp
strictly after the marker. A null marker SHALL be treated as has-update. The
user's own events SHALL never produce has-update, and events at or before the
marker SHALL never produce has-update.

#### Scenario: Unseen when never looked at

- **WHEN** the marker is null
- **THEN** the detector returns has-update

#### Scenario: Other person's commit after the marker is an update

- **WHEN** another user's commit landed after the marker instant
- **THEN** the detector returns has-update

#### Scenario: Own activity after the marker is not an update

- **WHEN** the only events after the marker are authored by `myLogin`
- **THEN** the detector returns no update

#### Scenario: Activity at or before the marker is not an update

- **WHEN** every other person's event has a timestamp at or before the marker
- **THEN** the detector returns no update

#### Scenario: Other person's comment or review after the marker is an update

- **WHEN** another user submitted a comment or a review after the marker instant
- **THEN** the detector returns has-update

### Requirement: Already-covered flag reflects other reviewers' submitted reviews

`CoveredFlag` SHALL return true when at least one submitted review has a
reviewer other than `myLogin`, and false otherwise. A pending review *request*
(no submitted review) SHALL NOT make a pull request covered.

#### Scenario: Another reviewer's review marks it covered

- **WHEN** another user has submitted any review (approved, changes-requested,
  or commented)
- **THEN** the flag is true

#### Scenario: Only pending requests does not mark it covered

- **WHEN** other reviewers are requested but none has submitted a review
- **THEN** the flag is false

#### Scenario: Only the user's own review does not mark it covered

- **WHEN** the only submitted reviews are authored by `myLogin`
- **THEN** the flag is false

### Requirement: Queue item carries identity and the derived outputs

The derivation SHALL produce, for each shown pull request, a `QueueItem`
carrying the pull request's identity (stable id, owner, repository, number,
title, URL, and the last-updated author and instant for display) together with
its membership state (`AwaitingFirstReview` or `AwaitingReReview`), its
has-update flag, and its already-covered flag. Hidden pull requests SHALL NOT
produce a `QueueItem`. The derivation SHALL NOT sort or group the items.

#### Scenario: Shown pull request yields a queue item

- **WHEN** a pull request derives to a shown membership state
- **THEN** a `QueueItem` is produced carrying that state plus the has-update and
  already-covered flags

#### Scenario: Hidden pull request yields no queue item

- **WHEN** a pull request derives to any hidden result (draft, closed, approved,
  or untracked)
- **THEN** no `QueueItem` is produced for it
