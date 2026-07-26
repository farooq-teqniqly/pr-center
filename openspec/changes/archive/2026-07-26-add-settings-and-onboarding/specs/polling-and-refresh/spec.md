# polling-and-refresh Specification

## MODIFIED Requirements

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

## ADDED Requirements

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
