## ADDED Requirements

### Requirement: A lock gate selects the screen from app lock state
The system SHALL read the app lock state before rendering the inbox and route to
one of three screens: Unlocked renders the review inbox; Locked renders the
unlock card; Uninitialized renders a placeholder directing the user to settings
to set a password and add tokens. The gate SHALL re-evaluate lock state on a
successful unlock and on a vault reset, and SHALL NOT poll.

#### Scenario: Unlocked shows the inbox
- **WHEN** the gate reads the app lock state and it is Unlocked
- **THEN** the review inbox is rendered

#### Scenario: Locked shows the unlock card
- **WHEN** the gate reads the app lock state and it is Locked
- **THEN** the unlock card is rendered instead of the inbox

#### Scenario: Uninitialized shows the settings placeholder
- **WHEN** the gate reads the app lock state and it is Uninitialized
- **THEN** a placeholder directing the user to settings is rendered, and no inbox is shown

### Requirement: The unlock card unlocks or resets the vault
The unlock card SHALL submit the entered password to the unlock use case. On a
successful unlock the gate SHALL re-evaluate to Unlocked; on a failed unlock the
card SHALL show a wrong-password message and remain on the unlock screen. The
card SHALL offer a reset action that clears the stored tokens and returns the
app to the Uninitialized state. The card SHALL NOT itself poll or trigger a
poll -- the unlock use case pokes the refresh trigger on success.

#### Scenario: Correct password unlocks
- **WHEN** the user submits the correct password on the unlock card
- **THEN** the unlock succeeds and the gate re-evaluates to the inbox

#### Scenario: Wrong password stays on the card
- **WHEN** the user submits an incorrect password
- **THEN** a wrong-password message is shown and the unlock card remains displayed

#### Scenario: Reset returns to uninitialized
- **WHEN** the user invokes the reset action on the unlock card
- **THEN** the stored tokens are cleared and the app returns to the Uninitialized state

### Requirement: The inbox groups and sorts the queue for the user
The inbox SHALL read the published queue snapshot and render its items grouped by
owner then repository. Within a group, items SHALL be ordered with those having
an update first, then most-recently-updated first (by the last-update instant).
Group order SHALL follow the configured owner order so the list is stable across
polls. Grouping and sorting SHALL be a pure projection over the snapshot,
evaluated relative to the user, with no derivation, facts access, or persistence
in the UI.

#### Scenario: Items grouped by owner then repository
- **WHEN** the inbox renders a snapshot with items across multiple owners and repositories
- **THEN** items appear grouped by owner and, within an owner, by repository

#### Scenario: Updated items sort first within a group
- **WHEN** a group contains items both with and without an update
- **THEN** the items with an update appear before those without, and within each set the most recently updated appears first

#### Scenario: Group order is stable across polls
- **WHEN** two consecutive snapshots carry the same owners
- **THEN** the groups render in the same configured owner order in both

### Requirement: A queue row renders each pull request's derived state
Each row SHALL render, relative to the user: an amber stripe and "Updated" badge
when the pull request has an update; a byline of who last touched it and when
(last-update author and relative time); reviewer roster chips colored by each
reviewer's state, with a distinct treatment for the user's own chip and for bot
reviewers; a covered decoration naming the covering reviewers when the pull
request is already covered; and two engagement/activity instants -- the user's
last-reviewed instant and the last-update instant. A pull request the user has
never reviewed has no update baseline, so it SHALL render without the update
stripe and badge. State SHALL never be conveyed by color alone -- the badge and
chip text carry the same meaning as text.

#### Scenario: Updated pull request shows the stripe and badge
- **WHEN** a row renders a pull request that has an update for the user
- **THEN** the amber stripe and "Updated" badge are shown

#### Scenario: Never-reviewed pull request shows no badge
- **WHEN** a row renders a pull request the user has never reviewed
- **THEN** no update stripe or badge is shown, and the row still appears in the list

#### Scenario: Roster chip marks the user and bots distinctly
- **WHEN** a row renders a roster containing the user and a bot reviewer
- **THEN** the user's chip carries the "me" treatment and the bot chip carries the bot treatment, each colored by its reviewer state

#### Scenario: Covered pull request names its coverers
- **WHEN** a row renders a pull request that is already covered
- **THEN** a covered decoration names the covering reviewers

#### Scenario: Byline is who-last-touched and when
- **WHEN** a row renders any pull request
- **THEN** the byline shows the last-update author and a relative time, with no activity-verb summary

### Requirement: Click-through opens the pull request without side effect
The pull request title SHALL be a plain anchor to the pull request's GitHub URL
that opens in a new tab. Clicking it SHALL NOT mutate any app or GitHub state,
SHALL NOT perform a live fetch, and SHALL NOT mark the pull request as seen -- no
mark-as-seen behavior exists. The update badge SHALL clear only on a later
published snapshot after the user reviews the pull request on GitHub.

#### Scenario: Title opens GitHub in a new tab
- **WHEN** the user clicks a pull request title
- **THEN** the pull request's GitHub URL opens in a new browser tab

#### Scenario: Click does not change state
- **WHEN** the user clicks a pull request title
- **THEN** no app or GitHub state changes, no live fetch occurs, and the update badge is unaffected by the click

### Requirement: The inbox surfaces per-owner status and failures
The inbox SHALL render a status chip per owner from the snapshot's owner
statuses. Any owner whose status is not ok SHALL raise a labeled error banner and
a stale indicator showing when that owner's data was last fresh, while that
owner's carried-forward rows SHALL still be shown. Owner chips SHALL remain
visible whether the queue is populated or empty.

#### Scenario: All owners ok
- **WHEN** the inbox renders a snapshot in which every owner status is ok
- **THEN** each owner shows an ok status chip and no error banner is shown

#### Scenario: A failing owner raises a banner and stale label
- **WHEN** the inbox renders a snapshot in which an owner status is not ok
- **THEN** a labeled error banner and a stale indicator for that owner are shown, and that owner's carried rows still appear

### Requirement: The inbox distinguishes empty from never-polled
The inbox SHALL render an "all caught up" state when a snapshot has been polled
and contains no items, keeping the owner status chips visible so a provable-empty
queue is distinct from a failed-fetch-empty one. When no snapshot has been
published since process start, the inbox SHALL render a distinct "polling has not
run yet" state.

#### Scenario: Polled and empty
- **WHEN** the inbox renders a published snapshot that contains no items
- **THEN** an "all caught up" state is shown with the owner status chips still visible

#### Scenario: Never polled since start
- **WHEN** the inbox renders while no snapshot has been published since process start
- **THEN** a distinct "polling has not run yet" state is shown, not the empty state

### Requirement: The inbox re-renders on publish without a timer
The inbox SHALL subscribe to the snapshot holder's change notification and
re-render from the newly published snapshot, marshaling to the render thread. The
inbox SHALL NOT run a UI polling timer, and SHALL unsubscribe when disposed.

#### Scenario: New snapshot re-renders the inbox
- **WHEN** a new snapshot is published while the inbox is displayed
- **THEN** the inbox re-renders from that snapshot without any timer

#### Scenario: Disposed inbox unsubscribes
- **WHEN** the inbox component is disposed
- **THEN** it unsubscribes from the holder's change notification

### Requirement: The inbox offers a manual refresh
The inbox SHALL provide a refresh action that pokes the refresh trigger to
request an immediate poll. The action SHALL NOT poll GitHub directly -- it only
requests a poll through the trigger the background loop owns.

#### Scenario: Manual refresh requests a poll
- **WHEN** the user invokes the inbox refresh action
- **THEN** the refresh trigger is poked to request an immediate poll
