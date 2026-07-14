## ADDED Requirements

### Requirement: Background poll loop runs on a configurable interval
The system SHALL run a background poll loop that refreshes the review queue on
a configurable interval, defaulting to 5 minutes, read from application
configuration until the settings change owns it.

#### Scenario: Interval elapses while unlocked
- **WHEN** the app is Unlocked and the poll interval elapses
- **THEN** the system performs a queue refresh

#### Scenario: Configured interval is honored
- **WHEN** a non-default poll interval is configured
- **THEN** the loop waits that interval between scheduled polls

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
facts, and run the derivers against the stored last-seen markers -- evaluating
everything relative to the user. The refresh SHALL then publish a new queue
snapshot containing the derived queue items and each owner's fetch status.

#### Scenario: Successful multi-owner refresh
- **WHEN** a refresh runs with multiple owners having stored tokens and all fetches succeed
- **THEN** the published snapshot contains the derived queue items of every owner and an ok status per owner

#### Scenario: Login resolved per owner per poll
- **WHEN** a refresh runs
- **THEN** the authenticated login is resolved for each owner during that poll, and the user-relative derivations for that owner's items use that login

#### Scenario: No owners configured
- **WHEN** a refresh runs while no owner tokens are stored
- **THEN** the system publishes an empty snapshot with no owner statuses, without calling GitHub

### Requirement: A per-owner fetch failure degrades only that owner
A fetch failure for one owner SHALL be recorded as that owner's fetch status in
the snapshot and SHALL NOT prevent other owners from being fetched, derived, and
published in the same refresh.

#### Scenario: One owner fails, others succeed
- **WHEN** a refresh runs and one owner's fetch reports a failure status while the others succeed
- **THEN** the published snapshot contains the successful owners' items, the failing owner's failure status, and no items for the failing owner

### Requirement: The queue snapshot is observable, including a never-polled state
The system SHALL expose the most recently published queue snapshot (derived
items, per-owner fetch statuses, and the instant the snapshot was taken) and
SHALL distinguish "never polled since process start" from "polled and empty".
Snapshots live in process memory only; no queue facts are persisted.

#### Scenario: Read before any poll
- **WHEN** the queue is requested before any refresh has completed since process start
- **THEN** the system reports an explicit never-polled result, not an empty queue

#### Scenario: Read after a poll
- **WHEN** the queue is requested after at least one refresh has completed
- **THEN** the system returns the latest published snapshot with its snapshot instant

#### Scenario: Snapshot replacement is atomic
- **WHEN** a refresh publishes a new snapshot while readers are reading
- **THEN** every reader observes either the old snapshot or the new one in full, never a mixture

### Requirement: One refresh trigger wakes the loop for manual refresh and unlock
The system SHALL provide a single refresh trigger that requests an immediate
poll. Trigger requests SHALL coalesce (many requests while a poll is running or
pending produce at most one subsequent poll), and polls SHALL never overlap.
A successful unlock SHALL poke this trigger so the first poll happens
immediately rather than waiting for the interval.

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

### Requirement: Mark-as-seen live-fetches the pull request and writes a fact-derived marker
On click-through, the system SHALL fetch fresh facts for that single pull
request and write its last-seen marker as the maximum activity timestamp in the
fetched facts (commits, comments, and reviews, including the user's own and
bots' -- the marker is a high-water mark of what existed when the user looked,
not an update judgment). The marker SHALL NOT be derived from the local wall
clock. When the live fetch returns no facts (pull request inaccessible or
gone), no marker SHALL be written.

#### Scenario: Marker set from fetched activity
- **WHEN** the user clicks through to a pull request and the live fetch returns facts
- **THEN** the last-seen marker is set to the maximum timestamp across the fetched commits, comments, and reviews

#### Scenario: Activity between poll and click is not lost
- **WHEN** another person's activity landed after the last poll but is present in the click-through live fetch
- **THEN** the marker covers that activity, and activity landing after the live fetch still derives as an update on the next poll

#### Scenario: Live fetch returns nothing
- **WHEN** the user clicks through and the live fetch reports the pull request inaccessible or gone
- **THEN** no marker is written and no error surfaces to the user

#### Scenario: Facts with no activity events
- **WHEN** the live fetch returns facts whose activity lists are all empty (defensive case)
- **THEN** the marker falls back to the facts' last-touch stamp, staying in GitHub's timestamp domain
