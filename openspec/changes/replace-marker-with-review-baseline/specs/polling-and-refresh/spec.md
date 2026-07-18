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

## REMOVED Requirements

### Requirement: Mark-as-seen live-fetches the pull request and writes a fact-derived marker

**Reason**: The click-through marker cleared the update indicator even when the
user opened and closed the pull request without reading it -- a silent miss, the
exact failure this tool exists to prevent. The update baseline now derives from
the user's latest review instant (see queue-derivation), which can only
over-notify, so no click-time write remains.

**Migration**: None. Click-through becomes a plain `target="_blank"` anchor to
the pull request's URL with no side effect; the update indicator clears on the
next poll after the user reviews on GitHub. The `MarkSeen` use case and its
click-time live fetch are deleted.
