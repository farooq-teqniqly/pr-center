## MODIFIED Requirements

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
the notification has exactly one raise site.

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
