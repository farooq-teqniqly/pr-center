# polling-and-refresh Specification

## Purpose
TBD - created by archiving change add-polling-and-refresh. Update Purpose after archive.
## Requirements
### Requirement: Background poll loop runs on a configurable interval
The system SHALL run a background poll loop that refreshes the review queue on
an interval read from stored application settings, defaulting to 5 minutes when
no interval has been stored. The allowed range is 5 minutes to 24 hours,
inclusive. The interval SHALL be read on each cycle rather than captured once at
startup, so an interval saved while the app is running takes effect on the next
scheduled poll without a restart. A stored value outside the allowed range SHALL
be clamped to the nearest allowed value and logged as a warning; it SHALL NOT
fail startup, because the only surface that can correct it lives inside the
running app. The interval SHALL be readable regardless of lock state -- it is
not secret and the loop needs it before it knows whether the app is unlocked.

#### Scenario: Interval elapses while unlocked
- **WHEN** the app is Unlocked and the poll interval elapses
- **THEN** the system performs a queue refresh

#### Scenario: Stored interval is honored
- **WHEN** a non-default interval is stored in settings
- **THEN** the loop waits that interval between scheduled polls

#### Scenario: No stored interval uses the default
- **WHEN** the loop reads the interval and no interval has been stored
- **THEN** the loop uses the 5-minute default

#### Scenario: A saved interval takes effect without a restart
- **WHEN** a new in-range interval is stored while the app is running
- **THEN** the next scheduled poll waits the new interval, with no restart required

#### Scenario: Out-of-range stored value is clamped, not fatal
- **WHEN** the loop reads a stored interval below 5 minutes or above 24 hours
- **THEN** the value is clamped to the nearest allowed value, a warning is logged, and the app continues running

#### Scenario: Interval is readable while locked
- **WHEN** the loop reads the interval while the app is Locked or Uninitialized
- **THEN** the read succeeds without requiring the vault key

### Requirement: Polling is gated on the app being unlocked
The system SHALL NOT poll GitHub while the app lock state is anything but
Unlocked. A wake (timer or refresh request) while not Unlocked SHALL be skipped
without error and without touching the published snapshot.

#### Scenario: Wake while locked
- **WHEN** the poll loop wakes and the app lock state is Locked or Uninitialized
- **THEN** no GitHub call is made and the published snapshot is unchanged

#### Scenario: Vault locked mid-poll
- **WHEN** a poll is in flight and the vault becomes locked (a locked-vault error escapes the fetch)
- **THEN** the system abandons that poll, logs a warning, leaves the previously published snapshot untouched, and resumes waiting

### Requirement: Queue refresh derives the queue from every owner with a token
A queue refresh SHALL enumerate the owners with a stored token, and for each
owner resolve the authenticated user's login, fetch that owner's review-queue
facts, and run the derivers -- deriving each pull request's update baseline from
its own facts (the user's latest review instant) -- evaluating everything
relative to the user. The refresh SHALL NOT read or write any stored last-seen
marker. The refresh SHALL then publish a new queue snapshot containing the
derived queue items and each owner's fetch status.

#### Scenario: Successful multi-owner refresh
- **WHEN** a refresh runs with multiple owners having stored tokens and all fetches succeed
- **THEN** the published snapshot contains the derived queue items of every owner and an ok status per owner

#### Scenario: Login resolved per owner per poll
- **WHEN** a refresh runs
- **THEN** the authenticated login is resolved for each owner during that poll, and the user-relative derivations for that owner's items use that login

#### Scenario: Update baseline derived from facts, not storage
- **WHEN** a refresh derives a pull request's has-update flag
- **THEN** the baseline is the user's latest review instant computed from that pull request's facts, and no stored marker is read for it

#### Scenario: No owners configured
- **WHEN** a refresh runs while no owner tokens are stored
- **THEN** the system publishes an empty snapshot with no owner statuses, without calling GitHub

### Requirement: A per-owner fetch failure degrades only that owner

A fetch failure for one owner SHALL be recorded as that owner's fetch status in
the snapshot and SHALL NOT prevent other owners from being fetched, derived, and
published in the same refresh.

A failed owner's items SHALL NOT vanish: the refresh SHALL carry that owner's
items forward from the previously published snapshot into the new one,
unchanged (their derived flags reflect the derivation at their original
fetch). Each owner status SHALL carry a last-fresh instant: null when the
owner's fetch succeeded in this snapshot (fresh as of the snapshot instant),
otherwise the instant the carried data was last successfully fetched -- the
previous snapshot's instant when the owner was fresh in it, else the previous
status's own last-fresh instant, so consecutive failures chain the original
instant forward. An owner that has never been successfully fetched has no
items to carry and a null last-fresh instant alongside its failure status. An
owner no longer enumerated by the vault SHALL NOT be carried -- it is no
longer polled, so its items and status leave the snapshot.

#### Scenario: One owner fails, others succeed

- **WHEN** a refresh runs and one owner's fetch reports a failure status while
  the others succeed
- **THEN** the published snapshot contains the successful owners' fresh items
  and the failing owner's failure status

#### Scenario: Failed owner's items are carried forward

- **WHEN** an owner's fetch fails and the previous snapshot contains items for
  that owner
- **THEN** the new snapshot contains those items unchanged, alongside the
  owner's failure status with the instant they were last fresh

#### Scenario: Consecutive failures keep the original fresh instant

- **WHEN** an owner's fetch fails in two or more consecutive refreshes
- **THEN** each new snapshot carries the items and the last-fresh instant of
  the last refresh in which that owner succeeded

#### Scenario: Recovery replaces carried items

- **WHEN** a previously failing owner's fetch succeeds again
- **THEN** the new snapshot contains that owner's freshly derived items and an
  ok status with a null last-fresh instant

#### Scenario: Never-fresh owner has no items to carry

- **WHEN** an owner's fetch fails and no previous snapshot contains items for
  it
- **THEN** the new snapshot contains the owner's failure status with a null
  last-fresh instant and no items for that owner

#### Scenario: Removed owner is not carried

- **WHEN** an owner with items in the previous snapshot is no longer
  enumerated by the vault
- **THEN** the new snapshot contains neither items nor a status for that owner

### Requirement: The queue snapshot is observable, including a never-polled state
The system SHALL expose the most recently published queue snapshot (derived
items, per-owner fetch statuses, and the instant the snapshot was taken) and
SHALL distinguish "never polled since process start" from "polled and empty".
Snapshots live in process memory only; no queue facts are persisted.

The snapshot holder SHALL raise a change notification each time a new snapshot
is published, after the reference swap, so an observer can re-read the current
snapshot without polling on a timer. The notification carries no payload -- a
subscriber reads the current snapshot in response. Raising happens on the
publishing (poll) thread; subscribers SHALL keep their handlers trivial and
marshal any UI work off that thread. There is exactly one publication point, so
the notification has exactly one raise site. Each subscriber SHALL be invoked in
isolation: a handler that throws SHALL be logged and skipped, and SHALL NOT
abort publication, prevent the remaining subscribers from being notified, or
propagate to the publishing poll loop.

#### Scenario: Read before any poll
- **WHEN** the queue is requested before any refresh has completed since process start
- **THEN** the system reports an explicit never-polled result, not an empty queue

#### Scenario: Read after a poll
- **WHEN** the queue is requested after at least one refresh has completed
- **THEN** the system returns the latest published snapshot with its snapshot instant

#### Scenario: Snapshot replacement is atomic
- **WHEN** a refresh publishes a new snapshot while readers are reading
- **THEN** every reader observes either the old snapshot or the new one in full, never a mixture

#### Scenario: Publish raises a change notification
- **WHEN** a refresh publishes a new snapshot and an observer is subscribed to the holder
- **THEN** the observer is notified after the swap, and reading the current snapshot in response returns the just-published snapshot

#### Scenario: No subscribers does not fault publishing
- **WHEN** a refresh publishes a new snapshot and no observer is subscribed
- **THEN** publishing completes normally and the snapshot is available to later readers

#### Scenario: A faulting subscriber does not abort publication
- **WHEN** a refresh publishes a new snapshot and one subscribed observer's handler throws
- **THEN** the failure is logged, the remaining subscribers are still notified, the snapshot is still published, and no exception reaches the polling loop

### Requirement: One refresh trigger wakes the loop for manual refresh and unlock
The system SHALL provide a single refresh trigger that requests an immediate
poll. Trigger requests SHALL coalesce (many requests while a poll is running or
pending produce at most one subsequent poll), and polls SHALL never overlap.
A successful unlock SHALL poke this trigger so the first poll happens
immediately rather than waiting for the interval. First-run setup, storing or
deleting an owner token, and saving a new poll interval SHALL each poke the
trigger on success, so a change to the polled set or the schedule is not delayed
by the wait already in flight. An action that fails or is rejected SHALL NOT
poke the trigger.

#### Scenario: Manual refresh while idle
- **WHEN** the trigger is poked while the app is Unlocked and no poll is running
- **THEN** a refresh starts promptly without waiting for the interval

#### Scenario: Pokes coalesce during an in-flight poll
- **WHEN** the trigger is poked multiple times while a poll is in flight
- **THEN** at most one additional refresh runs after the current one completes

#### Scenario: Unlock triggers an immediate poll
- **WHEN** the user unlocks the app successfully
- **THEN** the trigger is poked and a refresh starts promptly

#### Scenario: Failed unlock does not trigger
- **WHEN** an unlock attempt fails (wrong password)
- **THEN** the trigger is not poked

#### Scenario: First-run setup triggers an immediate poll
- **WHEN** first-run setup sets the app password and unlocks the app
- **THEN** the trigger is poked and a refresh starts promptly

#### Scenario: Token change triggers an immediate poll
- **WHEN** an owner token is stored or deleted successfully
- **THEN** the trigger is poked and a refresh starts promptly

#### Scenario: Interval save triggers an immediate poll
- **WHEN** a new in-range poll interval is stored
- **THEN** the trigger is poked and a refresh starts promptly

#### Scenario: A rejected change does not trigger
- **WHEN** a token save or interval save is rejected by validation
- **THEN** nothing is stored and the trigger is not poked

### Requirement: The poll interval is stored in the database, not in configuration
The system SHALL persist the poll interval in the local database as typed
application settings, and SHALL NOT read it from application configuration. The
absence of a stored interval SHALL mean the 5-minute default rather than an
error. Writing an interval SHALL accept only values within the allowed range,
rejecting anything outside it before it reaches storage. Reading and writing the
interval SHALL NOT require the vault key.

#### Scenario: Interval round-trips through storage
- **WHEN** an in-range interval is written and then read back
- **THEN** the read returns the written interval

#### Scenario: Absent settings read as the default
- **WHEN** the interval is read and no settings row exists
- **THEN** the read returns the 5-minute default without error and without creating a row

#### Scenario: Out-of-range write is rejected
- **WHEN** a write is attempted with an interval below 5 minutes or above 24 hours
- **THEN** the write is rejected and the stored value is unchanged

#### Scenario: Configuration no longer supplies the interval
- **WHEN** the app starts with a poll interval present in application configuration
- **THEN** the configured value is ignored and the stored interval (or the default) is used

