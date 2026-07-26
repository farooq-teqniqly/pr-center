# review-queue-ui Specification

## MODIFIED Requirements

### Requirement: A lock gate selects the screen from app lock state
The system SHALL read the app lock state before rendering and route to one of
three screens: Unlocked renders the gated content; Locked renders the unlock
card; Uninitialized renders a placeholder directing the user to settings to set
a password and add tokens, carrying a working link to the settings route. The
gate SHALL re-evaluate lock state on a successful unlock and on a vault reset,
and SHALL NOT poll. The Locked and Uninitialized screens SHALL be substitutable
by the caller so a second gated surface can supply its own, defaulting to the
unlock card and the placeholder when it does not.

#### Scenario: Unlocked shows the inbox
- **WHEN** the gate reads the app lock state and it is Unlocked
- **THEN** the review inbox is rendered

#### Scenario: Locked shows the unlock card
- **WHEN** the gate reads the app lock state and it is Locked
- **THEN** the unlock card is rendered instead of the inbox

#### Scenario: Uninitialized shows the settings placeholder
- **WHEN** the gate reads the app lock state and it is Uninitialized
- **THEN** a placeholder directing the user to settings is rendered, and no inbox is shown

#### Scenario: The placeholder links to the settings route
- **WHEN** the user follows the link on the Uninitialized placeholder
- **THEN** the settings screen is reached

#### Scenario: A caller supplies its own gated screens
- **WHEN** a gated surface supplies its own Locked and Uninitialized content
- **THEN** the gate renders that content for those states instead of the unlock card and the placeholder, and Unlocked still renders the gated content

### Requirement: The unlock card unlocks or resets the vault
The unlock card SHALL submit the entered password to the unlock use case. On a
successful unlock the gate SHALL re-evaluate to Unlocked; on a failed unlock the
card SHALL show a wrong-password message and remain on the unlock screen. The
card SHALL offer a reset action that states it destroys the app password as well
as every stored token, and that requires the user to type a fixed confirmation
word before the wipe runs. A reset that is cancelled, or confirmed with a value
that does not match the required word, SHALL wipe nothing. A confirmed reset
SHALL clear the stored tokens and the app password and return the app to the
Uninitialized state. The card SHALL NOT itself poll or trigger a poll -- the
unlock use case pokes the refresh trigger on success.

#### Scenario: Correct password unlocks
- **WHEN** the user submits the correct password on the unlock card
- **THEN** the unlock succeeds and the gate re-evaluates to the inbox

#### Scenario: Wrong password stays on the card
- **WHEN** the user submits an incorrect password
- **THEN** a wrong-password message is shown and the unlock card remains displayed

#### Scenario: Reset requires a typed confirmation
- **WHEN** the user invokes the reset action
- **THEN** a confirmation step appears stating that the app password and all stored tokens are destroyed, and nothing is wiped until the required word is typed

#### Scenario: Confirmed reset returns to uninitialized
- **WHEN** the user types the required confirmation word and confirms the reset
- **THEN** the stored tokens and the app password are cleared and the app returns to the Uninitialized state

#### Scenario: Mistyped confirmation wipes nothing
- **WHEN** the user confirms the reset with a value that does not match the required word
- **THEN** no reset is performed, the stored tokens and app password are unchanged, and the card stays on the confirmation step

#### Scenario: Cancelled confirmation wipes nothing
- **WHEN** the user cancels the reset confirmation
- **THEN** no reset is performed and the card returns to the unlock state
