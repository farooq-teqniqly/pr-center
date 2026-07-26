# settings-and-onboarding Specification

## ADDED Requirements

### Requirement: A settings screen exists and is gated on app lock state
The system SHALL provide a settings screen at `/settings` that reads the app
lock state before rendering and routes to one of three views: Uninitialized
renders the first-run setup card; Locked renders a short message directing the
user to unlock on the inbox, with a link back to it, and no token or interval
controls; Unlocked renders the owner tokens table and the poll interval control.
The screen SHALL re-evaluate lock state after a successful first-run setup. The
screen SHALL be reachable from the app's navigation and from the Uninitialized
placeholder on the inbox.

#### Scenario: Uninitialized shows the setup card
- **WHEN** the settings screen renders and the app lock state is Uninitialized
- **THEN** the first-run setup card is rendered, and no tokens table or interval control is shown

#### Scenario: Locked shows an unlock-first message
- **WHEN** the settings screen renders and the app lock state is Locked
- **THEN** a message directing the user to unlock is rendered with a link to the inbox, and no tokens table, interval control, or reset action is shown

#### Scenario: Unlocked shows tokens and interval
- **WHEN** the settings screen renders and the app lock state is Unlocked
- **THEN** the owner tokens table and the poll interval control are rendered

#### Scenario: Setup transitions the screen without a reload
- **WHEN** first-run setup completes successfully on the settings screen
- **THEN** the screen re-evaluates lock state and renders the Unlocked view

### Requirement: First-run setup sets the password and leaves the app unlocked
The system SHALL provide a first-run setup that sets the app password and, on
success, unlocks the app in the same action, so the user is not asked to
re-enter the password just typed. The setup SHALL also request an immediate
poll. Setup SHALL require a password of 8 to 32 characters and a confirmation
field whose value matches it; the system SHALL suggest mixed case, digits, and
symbols without requiring them. A rejected password SHALL NOT be sent to the
vault.

#### Scenario: Valid password initializes and unlocks
- **WHEN** the user submits a password of 8 to 32 characters with a matching confirmation and no app password has been set
- **THEN** the app password is set, the app becomes Unlocked, and an immediate poll is requested

#### Scenario: Password too short or too long is rejected
- **WHEN** the user submits a password shorter than 8 or longer than 32 characters
- **THEN** the setup card shows a length message, the vault is unchanged, and the app remains Uninitialized

#### Scenario: Mismatched confirmation is rejected
- **WHEN** the user submits a password whose confirmation field does not match it
- **THEN** the setup card shows a mismatch message, the vault is unchanged, and the app remains Uninitialized

#### Scenario: Weak-but-valid password is accepted with guidance
- **WHEN** the user submits an in-range password containing no digits or symbols
- **THEN** the setup proceeds, since mixed case, digits, and symbols are suggested and not required

### Requirement: The tokens table lists each owner with its saved instant and fetch status
The system SHALL render one row per owner that has a stored token, showing the
owner, the instant that owner's token was saved, and that owner's fetch status
projected from the published queue snapshot's owner statuses. The saved instant
SHALL render as an explicit unknown for a token row that predates saved-instant
recording. An owner with no status in the current snapshot SHALL render as not
yet polled rather than as a failure. The table SHALL NOT render the owner's
last-fresh instant -- staleness of carried rows is the inbox's concern. The
table SHALL NOT display any stored token, in whole or in part.

#### Scenario: Owners listed with status
- **WHEN** tokens are stored for one or more owners and the current snapshot carries statuses for them
- **THEN** each owner renders with its saved instant and its status from that snapshot

#### Scenario: A failing owner is diagnosed in the table
- **WHEN** the current snapshot reports a non-ok status for an owner with a stored token
- **THEN** that owner's row shows the failure status and its detail

#### Scenario: A just-added owner has no status yet
- **WHEN** an owner's token has been saved but no snapshot yet carries a status for that owner
- **THEN** the row renders a not-yet-polled state rather than a failure

#### Scenario: A pre-existing token row has no saved instant
- **WHEN** a stored token row has no recorded saved instant
- **THEN** the row renders an explicit unknown in place of the instant

#### Scenario: Tokens are never displayed
- **WHEN** the tokens table renders for any owner
- **THEN** no token value or fragment of one appears in the rendered output

### Requirement: The user can add, replace, and delete an owner token
The system SHALL let the user store a token for a new owner, replace the token
of an owner that already has one, and delete an owner's token. Each of these
SHALL request an immediate poll on success. Adding an owner IS storing its
token; there is no separate owner record. Deleting an owner's token SHALL remove
that owner from the polled set. The system SHALL NOT call GitHub from the
settings screen for any of these actions.

#### Scenario: Add an owner
- **WHEN** the user submits a new owner name and a token while Unlocked
- **THEN** the token is stored for that owner, an immediate poll is requested, and the owner appears in the table

#### Scenario: Replace an owner's token
- **WHEN** the user submits a token for an owner that already has one
- **THEN** the stored token for that owner is replaced, its saved instant is updated, and an immediate poll is requested

#### Scenario: Delete an owner
- **WHEN** the user deletes an owner from the table
- **THEN** that owner's token row is removed, an immediate poll is requested, and the owner is no longer polled

#### Scenario: Settings never calls GitHub
- **WHEN** the user adds, replaces, or deletes an owner token
- **THEN** no GitHub call is made from the settings screen, and the owner's fetch outcome arrives with the next published snapshot

### Requirement: Owner and token input are validated for shape only
The system SHALL reject an empty or whitespace-only owner name, an owner name
longer than 255 characters, an empty or whitespace-only token, and a token
longer than 512 characters, showing a message and storing nothing. The system
SHALL NOT validate the owner name against GitHub's login rules, SHALL NOT
validate the token's prefix or format, and SHALL NOT call GitHub to check
either. Whether an owner and token actually work is reported by the next poll's
owner status.

#### Scenario: Empty owner or token is rejected
- **WHEN** the user submits an empty or whitespace-only owner name or token
- **THEN** a message is shown and nothing is stored

#### Scenario: Over-long input is rejected
- **WHEN** the user submits an owner name longer than 255 characters or a token longer than 512 characters
- **THEN** a message is shown and nothing is stored

#### Scenario: An unrecognized owner name is accepted
- **WHEN** the user submits a well-formed but nonexistent owner name with a token
- **THEN** the token is stored without a GitHub check, and the owner's failure surfaces as its status in the next snapshot

### Requirement: The user can change the poll interval within the allowed range
The system SHALL render the current poll interval and let the user change it.
An interval below 5 minutes or above 24 hours SHALL be rejected with a message
stating the allowed range, and nothing SHALL be stored. A saved interval SHALL
take effect for the next scheduled poll and SHALL request an immediate poll, so
a shortened interval is not delayed by the wait already in flight.

#### Scenario: Current interval is shown
- **WHEN** the poll interval control renders
- **THEN** it shows the interval currently in effect and states the allowed range

#### Scenario: In-range interval is saved
- **WHEN** the user saves an interval between 5 minutes and 24 hours inclusive
- **THEN** the interval is stored, an immediate poll is requested, and subsequent scheduled polls use the new interval

#### Scenario: Out-of-range interval is rejected on write
- **WHEN** the user saves an interval below 5 minutes or above 24 hours
- **THEN** a message stating the allowed range is shown and the stored interval is unchanged
