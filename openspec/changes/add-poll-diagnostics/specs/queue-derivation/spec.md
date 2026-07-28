# queue-derivation Specification

## MODIFIED Requirements

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

`QueueItemDeriver.Derive` SHALL return a result carrying either the `QueueItem`
for a shown pull request or the `MembershipExclusion` for a hidden one, rather
than a nullable `QueueItem`. The exclusion reason is already computed by
`MembershipDeriver`, and discarding it forces any caller that wants to explain
why a pull request is absent to re-derive it. The result SHALL follow the same
shape as `MembershipResult`: factory methods for the shown and hidden cases, so
an invalid combination cannot form.

This SHALL NOT change what is shown. A hidden pull request still produces no
`QueueItem`, and the exclusion reason is reporting only -- no membership,
update, or covered decision reads it.

#### Scenario: Shown pull request yields a queue item

- **WHEN** a pull request derives to a shown membership state
- **THEN** a shown result is produced carrying that state plus the has-update
  flag, the last-reviewed instant, the roster, and the covering reviewers

#### Scenario: Hidden pull request yields no queue item

- **WHEN** a pull request derives to any hidden result (draft, closed,
  approved, or untracked)
- **THEN** no `QueueItem` is produced for it

#### Scenario: Hidden pull request reports why

- **WHEN** a pull request is hidden
- **THEN** the result carries the exclusion reason that hid it -- draft, closed
  or merged, approved, or untracked -- matching the reason `MembershipDeriver`
  produced

#### Scenario: Never reviewed is explicit

- **WHEN** the user has no submitted review in the pull request's facts
- **THEN** the queue item's last-reviewed instant is null (rendered as "never"
  by the UI, not as a zero timestamp)

#### Scenario: Last reviewed reflects the user's latest review

- **WHEN** the user has submitted reviews on the pull request
- **THEN** the queue item's last-reviewed instant is the greatest submitted
  timestamp among them, whatever their states
