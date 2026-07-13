# app-lock Specification

## Purpose
TBD - created by archiving change add-token-vault-and-lock. Update Purpose after archive.
## Requirements
### Requirement: App lock has three runtime states
The system SHALL model the app lock as a runtime state machine with exactly
three states -- `Uninitialized` (no app password set), `Locked` (password set,
key not in memory), and `Unlocked` (derived key held in memory) -- derived from
whether a security row exists and whether a key is currently held.

#### Scenario: Fresh install reports Uninitialized
- **WHEN** the app starts and no security row exists
- **THEN** the lock state is Uninitialized

#### Scenario: Configured app starts Locked
- **WHEN** the app starts and a security row exists but no key is in memory
- **THEN** the lock state is Locked

#### Scenario: Unlocked only with a key in memory
- **WHEN** a derived key is held in memory after a successful unlock
- **THEN** the lock state is Unlocked

### Requirement: Unlock derives and holds the key
The system SHALL transition from Locked to Unlocked only when a supplied password
passes sentinel verification, at which point it holds the derived key in memory;
a wrong password SHALL leave the state Locked with no key retained.

#### Scenario: Successful unlock
- **WHEN** a correct password is supplied while Locked
- **THEN** the system derives the key, holds it in memory, and reports Unlocked

#### Scenario: Failed unlock
- **WHEN** an incorrect password is supplied while Locked
- **THEN** the system remains Locked and retains no key

#### Scenario: Unlock rejected when Uninitialized
- **WHEN** an unlock is attempted while Uninitialized
- **THEN** the system rejects it, since no password has been set

#### Scenario: Unlock while already Unlocked is an idempotent success
- **WHEN** an unlock is attempted while already Unlocked
- **THEN** the system returns success without re-deriving the key or replacing the held key, and the state stays Unlocked

### Requirement: The decrypted key is shared and held until the process stops
The system SHALL hold the decrypted key in a single process-wide holder shared
across all Blazor circuits and browser tabs, with no idle auto-lock; the key
SHALL be discarded only when the process stops or the vault is reset.

#### Scenario: Key shared across tabs
- **WHEN** the vault is unlocked in one browser tab
- **THEN** other tabs and circuits observe the Unlocked state without a separate unlock

#### Scenario: No idle timeout
- **WHEN** the vault has been unlocked and the app sits idle
- **THEN** the state remains Unlocked until the process stops

#### Scenario: Restart returns to Locked
- **WHEN** the process restarts while a security row exists
- **THEN** the state is Locked and the previous in-memory key is gone

### Requirement: Reset returns the lock to Uninitialized
The system SHALL, on vault reset, discard any in-memory key and report
`Uninitialized`, so the next step is setting a new password.

#### Scenario: Reset while Unlocked
- **WHEN** the vault is reset while Unlocked
- **THEN** the in-memory key is discarded and the state becomes Uninitialized

