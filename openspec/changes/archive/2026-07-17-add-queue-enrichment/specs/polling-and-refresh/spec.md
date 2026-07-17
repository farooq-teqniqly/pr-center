# polling-and-refresh Delta

## MODIFIED Requirements

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
