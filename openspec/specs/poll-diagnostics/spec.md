# poll-diagnostics Specification

## Purpose
TBD - created by archiving change add-poll-diagnostics. Update Purpose after archive.
## Requirements
### Requirement: Every refresh produces exactly one diagnostics record

The system SHALL produce one poll diagnostics record per refresh, built during
that refresh and handed to the configured sinks once, at the end of the refresh.
The record SHALL carry a poll-level part -- a poll identifier, the instant the
refresh started, the instant it ended, its outcome, the number of configured
owners, and the number of items published -- and one owner-level part per
configured owner.

The record SHALL be produced in exactly one place. No component other than the
refresh SHALL construct or emit one, so a later sink (for example
OpenTelemetry) consumes the same record rather than instrumenting the refresh a
second time.

The poll identifier SHALL be a value stable enough to correlate the record with
a trace outside this process, independent of any local row identifier.

The poll-level part SHALL also carry the configured owners themselves, captured
from the owner enumeration directly rather than assembled from the owner rows.
The count SHALL be derived from that same capture, and SHALL be absent when the
refresh failed before enumerating the owners. Zero and absent are different
claims: zero owners is a valid configuration that publishes an empty queue,
while an absent count means the owner list could not be read at all. Recording
the failure as zero would present a broken vault as an empty one.

The capture point is load-bearing. The owner rows are produced by the same
machinery a reader is using this record to diagnose, so a record whose only
account of the configured owners comes from those rows cannot expose a fault in
producing them. Taken from the enumeration itself, the list is an independent
witness, and an owner present in one and absent from the other is a detectable
defect rather than an invisible one.

#### Scenario: A successful refresh produces one record

- **WHEN** a refresh completes over any number of owners
- **THEN** exactly one diagnostics record is emitted, carrying one owner-level part per configured owner

#### Scenario: The published count is the deduplicated count

- **WHEN** two owners both return the same pull request and the refresh publishes one item for it
- **THEN** the record's published count is the number of items in the published snapshot, which is less than the sum of the owners' derived counts

#### Scenario: No owners configured is recorded as zero

- **WHEN** a refresh runs with no owner tokens stored and publishes an empty snapshot
- **THEN** the record's configured-owner count is zero, not absent, and it carries no owner rows

#### Scenario: An unreadable owner list is recorded as absent

- **WHEN** a refresh fails while enumerating the configured owners
- **THEN** the record's configured owners and their count are both absent, distinguishing it from a poll that found no owners

#### Scenario: The configured owners come from the enumeration, not the rows

- **WHEN** a refresh enumerates its configured owners and goes on to produce owner rows
- **THEN** the record's configured owners are those the enumeration returned, so an owner missing from the rows still appears in the list

### Requirement: The record is written on every exit path of a refresh

The system SHALL write the diagnostics record whether the refresh succeeded,
aborted because the vault locked, was canceled by shutdown, or faulted. The
outcome SHALL be recorded distinctly for each of these cases.

The write SHALL NOT use the caller's cancellation token, because on a shutdown
cancellation that token is already canceled and the write would fail on the very
path this requirement exists for. The write SHALL be bounded by its own timeout
so a blocked write cannot consume the host's shutdown budget.

A refresh that fails before its owners are enumerated SHALL still record a
poll-level row, with no owner rows.

#### Scenario: A vault lock mid-refresh is recorded

- **WHEN** the vault locks part-way through a refresh and the refresh aborts without publishing
- **THEN** the record is written with the aborted-by-lock outcome and no published count

#### Scenario: Shutdown cancellation is recorded

- **WHEN** the refresh is canceled by the host shutting down
- **THEN** the record is written with the canceled outcome, using a cancellation token other than the canceled caller's

#### Scenario: A failure to enumerate owners is recorded

- **WHEN** enumerating the configured owners throws before any owner is polled
- **THEN** a record is written with the faulted outcome, an absent configured-owner count, and no owner rows

#### Scenario: A publish failure is recorded

- **WHEN** publishing the snapshot throws after every owner has been polled
- **THEN** the record is written with the faulted outcome, carrying every owner row and no published count

### Requirement: Every configured owner is accounted for in every poll

The record SHALL contain one owner row for every configured owner, including an
owner whose fetch failed, an owner whose rows were carried over stale, and an
owner the refresh never reached because it aborted first.

An owner never reached SHALL be recorded with a distinct not-polled status, a
null start instant, and null counts. A null count and a zero count SHALL remain
distinct: null means the owner was never asked, zero means the answer was empty.

#### Scenario: An aborted poll shows where it stopped

- **WHEN** a refresh polls the first of three owners and then aborts
- **THEN** the record carries three owner rows: one polled, and two with the not-polled status, null start instants, and null counts

#### Scenario: An empty owner is not a null owner

- **WHEN** an owner is polled successfully and GitHub returns no pull requests
- **THEN** that owner's row carries a successful status with counts of zero, not null counts

#### Scenario: A failed owner records what it carried over

- **WHEN** an owner's fetch fails and its items are carried over from the previous snapshot
- **THEN** that owner's row carries the failure status and detail, null fetch counts, and a carried-over count equal to the number of items carried

### Requirement: An owner row records identity, outcome, counts, and rate limit

Each owner row SHALL carry:

- the owner, and the login the owner's token resolved to during this poll (null
  when login resolution did not complete);
- the instants that owner's poll started and ended;
- the fetch status and its detail;
- the node count returned by each of the two discovery searches, the count after
  the searches are unioned within the owner, the count surviving derivation, and
  the count of items carried over from a previous snapshot -- each null when not
  applicable;
- the count of pull requests excluded by derivation, broken down by exclusion
  reason;
- the rate-limit reading for that fetch;
- the compact identifiers of the pull requests the owner contributed;
- how many of those pull requests belong to an owner other than this one.

The resolved login SHALL be recorded per owner per poll, because two owners
resolving to the same login is how one pull request reaches the queue twice, and
that is not observable from the counts alone.

The foreign-item count SHALL be the number of contributed identifiers whose
owner is not the row's owner. Owner-queue discovery is not scoped to the owner,
so a token that can see another configured owner's repositories contributes that
owner's pull requests; the count names which token is reaching across, which the
poll-level overlap total cannot. A non-zero count is expected and not a fault.

To stay within the baseline parameter limit, these SHALL be grouped into
cohesive sub-records rather than a flat parameter list.

#### Scenario: Two owners resolving to the same login is visible

- **WHEN** two configured owners' tokens both resolve to the same login in one poll
- **THEN** both owner rows record that same resolved login

#### Scenario: The derivation cliff is explained by reason

- **WHEN** an owner's union contains pull requests that derivation hides
- **THEN** that owner's row records how many were hidden for each reason -- draft, closed or merged, approved, and untracked -- and those counts plus the derived count equal the union count

#### Scenario: A token reaching into another owner is named

- **WHEN** an owner's fetch contributes pull requests belonging to a different configured owner
- **THEN** that owner's row records how many of its contributed identifiers belong to another owner

#### Scenario: An owner contributing only its own pull requests

- **WHEN** every pull request an owner contributed belongs to that owner
- **THEN** its foreign-item count is zero

#### Scenario: Login resolution failure yields a null login

- **WHEN** resolving an owner's authenticated login throws
- **THEN** that owner's row carries a failure status, a null resolved login, and null fetch counts

### Requirement: Diagnostics store identifiers and counts only

The system SHALL NOT persist any response body, pull request title, comment
text, review body, or any other free text originating from GitHub. Pull requests
SHALL be recorded as compact identifiers of the form `owner/repo#number` only.

The only free text a record may carry is the fetch-status detail the system
itself composes, which is transport-neutral by construction and contains no
GitHub payload.

An owner row SHALL therefore be safe to paste verbatim into a public issue.

#### Scenario: Pull requests are recorded as identifiers

- **WHEN** an owner contributes pull requests to the queue
- **THEN** the row records their `owner/repo#number` identifiers and no titles, bodies, or author-authored text

#### Scenario: A GitHub error detail carries no payload

- **WHEN** an owner's fetch fails and a detail is recorded
- **THEN** the detail is one of the system's own transport-neutral messages, not raw response text

### Requirement: Diagnostics are retained as a ring buffer of whole polls

The system SHALL retain the most recent N polls and SHALL remove older polls
whole, evicting by poll rather than by row, so a poll covering many owners does
not evict more history than a poll covering few. N SHALL be a fixed value in
this version.

The insert of a new poll and the eviction of the polls it displaces SHALL be
atomic: an observer SHALL never see a state in which the new poll is present and
the eviction has not run, or in which a poll is half-removed.

#### Scenario: The oldest poll is evicted whole

- **WHEN** a poll is recorded while N polls are already stored
- **THEN** the single oldest poll and all of its owner rows are removed, and every other poll is untouched

#### Scenario: Eviction is not weighted by owner count

- **WHEN** the stored polls have differing owner counts
- **THEN** retention keeps the same number of polls regardless of how many owner rows each contains

### Requirement: A sink failure never affects a poll

The system SHALL treat diagnostics as best-effort. A sink that throws SHALL be
logged at warning or above and SHALL NOT fail the refresh, SHALL NOT prevent
other sinks from being written, and SHALL NOT replace an exception already
propagating out of the refresh.

#### Scenario: A throwing sink leaves the refresh outcome intact

- **WHEN** a sink throws while writing the record for a successful refresh
- **THEN** the refresh still reports success, the snapshot is still published, and a warning is logged

#### Scenario: A throwing sink does not mask a fault

- **WHEN** a refresh is faulting and a sink throws while the record is written
- **THEN** the original exception continues to propagate and the sink failure is logged separately

#### Scenario: One failing sink does not block another

- **WHEN** two sinks are configured and the first throws
- **THEN** the second is still written

### Requirement: Diagnostics are write-only to the refresh and derivation paths

The interface the refresh writes through SHALL expose no read member. Reading
diagnostics SHALL require a separate reader abstraction, and no type under the
queue-refresh or derivation namespaces SHALL depend on it.

This is a design invariant, not a convenience: membership is derived each poll
as a pure function of current GitHub facts, and a deriver that could read stored
diagnostics would reintroduce stored transition state.

#### Scenario: The write path cannot read

- **WHEN** the refresh holds the diagnostics sink abstraction
- **THEN** no member on it returns stored diagnostics

#### Scenario: Derivation does not reference the reader

- **WHEN** the queue-refresh and derivation types are inspected for dependencies
- **THEN** none of them references the diagnostics reader

### Requirement: Recent polls are readable from inside the app

The system SHALL provide a read surface listing the most recent polls newest
first, reachable from the settings screen, together with an action that copies a
poll's rows in a form suitable for pasting into an issue.

The list SHALL be grouped by poll, with each poll's owner rows available beneath
its summary line and collapsed by default. The system SHALL NOT require
navigating away from the list to read a poll's owner rows: the reader SHALL
return whole polls, and selecting one SHALL NOT issue a further read.

Each poll's summary line SHALL carry the instant the poll ran, its outcome, how
many of its configured owners were polled successfully out of the total, and the
number of items it published. The published count is on the summary line
deliberately: scanning that one column down the list is how a reader sees what
moved between polls, which is the question a single poll's rows cannot answer.

The owner ratio SHALL make a shortfall legible without expanding the poll: a
poll that polled fewer owners than it had configured is the at-a-glance signal
that something went wrong, and it SHALL be distinguishable from a poll whose
owner count could not be read at all.

A poll recorded with the canceled outcome SHALL be presented as an incomplete
poll rather than as a failure, since a host shutdown mid-poll is routine.

#### Scenario: The most recent polls are listed

- **WHEN** the user opens the diagnostics view
- **THEN** the most recent polls are listed newest first, each showing its instant, outcome, polled-owner ratio, and published count

#### Scenario: Owner rows open without a further read

- **WHEN** the user expands a poll in the list
- **THEN** that poll's owner rows are shown, and no additional read of the diagnostics store is issued

#### Scenario: A shortfall is visible without expanding

- **WHEN** a poll polled fewer owners than it had configured
- **THEN** its summary line shows the shortfall in the owner ratio, without the owner rows being expanded

#### Scenario: An absent owner count is not shown as zero

- **WHEN** a poll's configured-owner count is absent because the owner list could not be read
- **THEN** the summary line renders the ratio as unknown, not as zero out of zero

#### Scenario: Owner rows start collapsed

- **WHEN** the diagnostics view first renders
- **THEN** every poll's owner rows are collapsed, so the published-count column is scannable down the list

#### Scenario: A poll's rows can be copied

- **WHEN** the user invokes the copy action for a poll
- **THEN** that poll's identifiers, counts, and statuses are copied as text containing no GitHub payload

#### Scenario: No polls recorded yet

- **WHEN** the diagnostics view renders and no poll has been recorded
- **THEN** an explicit empty state is shown, distinguishable from a failure to read

### Requirement: A poll whose owner rows disagree with its configured owners is surfaced as a defect

The read surface SHALL compare each poll's owner rows against its recorded
configured owners and SHALL surface any disagreement -- a configured owner with
no row, or a row for an owner that was not configured. Unlike overlapping
owners, this is not an expected condition: every configured owner is required to
have a row, so a disagreement means the record was produced incorrectly and the
rest of that poll's numbers cannot be trusted.

A poll whose configured owners are absent SHALL NOT be surfaced as disagreeing,
since there is nothing to compare against.

#### Scenario: A missing owner row is surfaced

- **WHEN** a poll's configured owners include an owner that has no owner row
- **THEN** the poll is surfaced as disagreeing, naming that owner

#### Scenario: An unexpected owner row is surfaced

- **WHEN** a poll has an owner row for an owner absent from its configured owners
- **THEN** the poll is surfaced as disagreeing, naming that owner

#### Scenario: A consistent poll is not surfaced

- **WHEN** a poll's owner rows correspond exactly to its configured owners
- **THEN** the poll carries no disagreement indication

#### Scenario: An absent owner list is not a disagreement

- **WHEN** a poll's configured owners are absent because the enumeration failed
- **THEN** the poll is not surfaced as disagreeing

### Requirement: A poll whose owners overlap is marked, not flagged as an error

The read surface SHALL mark a poll whose owner rows together derived more items
than the poll published, and SHALL show the across-owner derived total alongside
the published count. Such a poll had at least one pull request reach the queue
from more than one owner, and the mark explains the discrepancy where it is
visible rather than leaving it to read as an arithmetic error.

The mark SHALL NOT be presented as a failure, an error, or a warning about the
poll's health. Owner-queue discovery is not scoped to the owner, so a token that
can see another configured owner's repositories produces this overlap routinely
and correctly. The mark means the counts will not sum the way a reader expects,
and says why.

#### Scenario: An overlapping poll is marked

- **WHEN** two owners in one poll each derived the same pull request and the poll published it once
- **THEN** that poll is marked as having overlapping owners, showing both the across-owner derived total and the published count

#### Scenario: A non-overlapping poll is not marked

- **WHEN** every pull request in a poll was derived by exactly one owner
- **THEN** the poll carries no overlap mark

#### Scenario: The mark is not an error state

- **WHEN** a poll is marked as having overlapping owners and its outcome is successful
- **THEN** the poll is still presented as a successful poll, and the mark is not rendered as an error or a failure

