# state-store Specification

## REMOVED Requirements

### Requirement: Last-seen markers persist per pull request and are never deleted

**Reason**: The update baseline is now derived from the user's latest review
instant each poll (see queue-derivation), so no per-pull-request read state is
stored. `IStateStore` (last-seen members), the `LastSeenMarker` entity, and
`StateStore` are deleted, and a forward migration drops the marker table.

**Migration**: None. The marker table is dropped by a new forward migration
(EF migrations are forward-only; the shipped table cannot be un-shipped by
deleting the entity). No data is preserved -- there was never durable value in
the marker beyond the update baseline it now derives.

### Requirement: Schema is created and evolved through migrations applied at startup

**Reason**: This requirement is worded around the marker schema, which is
removed. The migrations-and-startup-apply foundation itself is retained in code
and is exercised by `token-vault`, which carries its own schema.

**Migration**: None. `PrCenterDbContext`, the EF migrations infrastructure, and
startup migration remain; only the marker schema they described is dropped.
Schema evolution for remaining tables is owned by `token-vault`.

### Requirement: SQLite connection is configured for concurrent writes and fail-fast

**Reason**: The concurrent-writer scenario this requirement described (a
click-through marker write racing a background-poll marker write) no longer
exists once `MarkSeen` and `StateStore` are removed.

**Migration**: None. The WAL journal mode, busy timeout, and command timeout
remain configured on the shared `PrCenterDbContext` and continue to apply to
`token-vault`'s writes; the requirement text is retired with the marker
capability.

### Requirement: Sensitive EF diagnostics are enabled only in Development

**Reason**: A foundation concern of the shared `PrCenterDbContext`, retained in
code but no longer owned by the removed `state-store` capability.

**Migration**: None. The Development-only sensitive-logging behavior stays on
the shared context and now falls under `token-vault`'s ownership.

### Requirement: Integration tests exercise the real SQLite file

**Reason**: The marker round-trip this requirement verified is removed. The
real-SQLite integration-test harness itself is retained and reused by
`token-vault`'s persistence tests.

**Migration**: None. The reusable real-file harness remains in the test project;
only the marker round-trip test that motivated it is removed.
