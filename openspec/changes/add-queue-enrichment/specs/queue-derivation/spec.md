# queue-derivation Delta

## ADDED Requirements

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

## MODIFIED Requirements

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
- the user's engagement: when the user last looked (the last-seen marker
  instant, null when never looked) and when the user last reviewed (the
  greatest submitted timestamp among the user's reviews regardless of their
  state, null when the user has no review in the facts);
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
  flag, the engagement instants, the roster, and the covering reviewers

#### Scenario: Hidden pull request yields no queue item

- **WHEN** a pull request derives to any hidden result (draft, closed,
  approved, or untracked)
- **THEN** no `QueueItem` is produced for it

#### Scenario: Never looked and never reviewed are explicit

- **WHEN** the user has no last-seen marker for the pull request and no
  submitted review in its facts
- **THEN** the queue item's last-looked and last-reviewed instants are both
  null (rendered as "never" by the UI, not as a zero timestamp)

#### Scenario: Last reviewed reflects the user's latest review

- **WHEN** the user has submitted reviews on the pull request
- **THEN** the queue item's last-reviewed instant is the greatest submitted
  timestamp among them, whatever their states
