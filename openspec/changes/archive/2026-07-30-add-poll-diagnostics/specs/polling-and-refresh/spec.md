# polling-and-refresh Specification

## MODIFIED Requirements

### Requirement: Queue refresh derives the queue from every owner with a token

A queue refresh SHALL enumerate the owners with a stored token, and for each
owner resolve the authenticated user's login, fetch that owner's review-queue
facts, and run the derivers -- deriving each pull request's update baseline from
its own facts (the user's latest review instant) -- evaluating everything
relative to the user. The refresh SHALL NOT read or write any stored last-seen
marker. The refresh SHALL then publish a new queue snapshot containing the
derived queue items and each owner's fetch status.

Owner-queue discovery is not scoped to the owner, so one owner's token can return
pull requests belonging to another configured owner. The published snapshot SHALL
therefore contain at most one item per pull request id across all owners: a
freshly fetched item SHALL win over an item carried over from a failed owner, and
between two items of the same freshness the first owner in enumeration order
SHALL win. Deduplication SHALL NOT affect owner statuses -- every enumerated owner
still has a status, including one whose every item was deduplicated away.

Enumerating the owners SHALL happen inside the refresh's own failure handling, so
a failure to enumerate them is recorded as a failed poll rather than escaping
before the refresh has any record of itself.

#### Scenario: Successful multi-owner refresh
- **WHEN** a refresh runs with multiple owners having stored tokens and all fetches succeed
- **THEN** the published snapshot contains the derived queue items of every owner and an ok status per owner

#### Scenario: A pull request two owners both return appears once
- **WHEN** two owners' fetches both return the same pull request
- **THEN** the published snapshot contains one item for it, and an ok status for each of the two owners

#### Scenario: A fresh item wins over a colliding stale carry-over
- **WHEN** an owner's fetch fails and its carried-over items include a pull request another owner returned fresh in the same refresh
- **THEN** the published snapshot contains only the freshly fetched item for that pull request, alongside the failing owner's status

#### Scenario: Login resolved per owner per poll
- **WHEN** a refresh runs
- **THEN** the authenticated login is resolved for each owner during that poll, and the user-relative derivations for that owner's items use that login

#### Scenario: Update baseline derived from facts, not storage
- **WHEN** a refresh derives a pull request's has-update flag
- **THEN** the baseline is the user's latest review instant computed from that pull request's facts, and no stored marker is read for it

#### Scenario: No owners configured
- **WHEN** a refresh runs while no owner tokens are stored
- **THEN** the system publishes an empty snapshot with no owner statuses, without calling GitHub

## ADDED Requirements

### Requirement: A refresh records diagnostics for the poll it performed

A queue refresh SHALL build a poll diagnostics record as it runs and SHALL hand
it to the configured diagnostics sinks before returning, on every exit path --
success, an abort because the vault locked, a shutdown cancellation, and an
unhandled fault.

The record SHALL be written while the refresh's own dependency scope is still
alive, so the write cannot outlive the storage it writes through.

Writing diagnostics SHALL NOT change what the refresh publishes, what it returns
to the poll loop, or how a per-owner failure degrades. Diagnostics are an
observation of the refresh, never an input to it.

The refresh SHALL NOT read stored diagnostics for any purpose.

#### Scenario: A successful refresh records its poll

- **WHEN** a refresh completes and publishes a snapshot
- **THEN** a diagnostics record is written carrying the successful outcome, the published item count, and one row per configured owner

#### Scenario: An aborted refresh records its poll

- **WHEN** the vault locks mid-refresh and the refresh returns the aborted outcome without publishing
- **THEN** a diagnostics record is written carrying the aborted outcome, and the previously published snapshot is still untouched

#### Scenario: Diagnostics do not alter the refresh outcome

- **WHEN** a refresh runs with diagnostics sinks configured
- **THEN** the published snapshot and the returned outcome are the same as they would be with no sink configured

#### Scenario: A failing sink does not fail the refresh

- **WHEN** a diagnostics sink throws while the record is written for a successful refresh
- **THEN** the refresh still reports success and the failure is logged at warning
