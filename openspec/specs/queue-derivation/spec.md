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

### Requirement: Reviewer roster is derived from requests and submitted reviews

The derivation SHALL produce, for each shown pull request, a reviewer roster:
one entry per reviewer, where a reviewer is any directly requested reviewer or
any reviewer with a submitted review in the facts. Each entry SHALL carry the
reviewer's login, a roster state, whether the reviewer is a bot, and whether
the entry is the user (compared with the existing login-comparison rules,
never raw string comparison).

The roster state SHALL be:

- **Pending** for a requested reviewer with no submitted review (`IsBot`
  false -- requested reviewers arrive as plain logins, so actor type is
  unknowable before a review is submitted).
- Otherwise the state of the reviewer's latest submitted review (approved /
  changes-requested / commented), with the review's `IsBot` flag. Dismissed
  reviews never reach the facts (the adapter omits them), so the latest review
  in the facts is the latest standing review by construction.

A reviewer both requested and with submitted reviews SHALL appear once, with
the latest review's state. Bot reviewers SHALL stay in the roster (flagged) --
filtering or styling them is presentation. The roster SHALL NOT be sorted; the
user's entry SHALL be present like any other so the UI can decorate it.

#### Scenario: Requested reviewer with no review is pending

- **WHEN** a login appears in the requested reviewers and has no submitted
  review
- **THEN** the roster carries that login as Pending with `IsBot` false

#### Scenario: Reviewer takes the latest review's state

- **WHEN** a reviewer has multiple submitted reviews
- **THEN** the roster carries one entry for that reviewer with the state of
  the review with the greatest submitted timestamp

#### Scenario: Requested and reviewed appears once

- **WHEN** a login is both a requested reviewer and has a submitted review
- **THEN** the roster carries exactly one entry for it, with the review's
  state, not Pending

#### Scenario: Bot reviewer is flagged, not filtered

- **WHEN** a review with `IsBot` true is in the facts
- **THEN** the roster carries that reviewer with its state and `IsBot` true

#### Scenario: The user's own entry is marked

- **WHEN** the user is a requested reviewer or has a submitted review
- **THEN** the roster carries the user's entry with its is-me flag true

### Requirement: Already-covered flag reflects other reviewers' submitted reviews

`CoveredFlag` SHALL identify the covering reviewers: the distinct logins of
submitted **human** reviews (`IsBot` false) whose reviewer is not the user.
A pull request is already covered when at least one covering reviewer exists;
the covered indicator SHALL be derived from the covering-reviewer list, not
carried as an independent flag. A pending review *request* (no submitted
review) SHALL NOT make a pull request covered, and a bot review SHALL NOT
make a pull request covered -- a bot review is not human coverage. (Dismissed
reviews never reach the facts; the adapter omits them.)

#### Scenario: Another reviewer's review marks it covered

- **WHEN** another human has submitted any review (approved,
  changes-requested, or commented)
- **THEN** the covering reviewers contain that login and the pull request is
  covered

#### Scenario: Covering reviewers are distinct logins

- **WHEN** another human has submitted multiple reviews on the pull request
- **THEN** the covering reviewers contain that login exactly once

#### Scenario: Only pending requests does not mark it covered

- **WHEN** other reviewers are requested but none has submitted a review
- **THEN** the covering reviewers are empty and the pull request is not
  covered

#### Scenario: Only the user's own review does not mark it covered

- **WHEN** the only submitted reviews are authored by the user
- **THEN** the covering reviewers are empty and the pull request is not
  covered

#### Scenario: Only bot reviews does not mark it covered

- **WHEN** the only submitted reviews by others have `IsBot` set
- **THEN** the covering reviewers are empty and the pull request is not
  covered

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

