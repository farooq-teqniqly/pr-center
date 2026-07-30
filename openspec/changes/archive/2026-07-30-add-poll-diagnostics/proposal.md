# Proposal: add-poll-diagnostics

## Why

When the queue shows something unexpected, there is no way to see what GitHub
actually returned. A duplicate row for `farooq-teqniqly/pr-center#42`
([#43](https://github.com/farooq-teqniqly/pr-center/issues/43), fixed in
[#46](https://github.com/farooq-teqniqly/pr-center/issues/46)) took several
refresh cycles to reproduce, and the question "which owner produced the second
copy, and did both owners resolve to the same login?" could not be answered
after the fact. Logs are ephemeral in a container and nothing about a poll
survives a restart.

This change records what each poll saw, as a ring buffer of the last N polls,
redacted by construction so a row is safe to paste into an issue. It is the
instrumentation point `add-observability` (#9) builds on rather than a
throwaway: that change adds an OpenTelemetry sink over the same record instead
of instrumenting the poll a second time. Closes
[#44](https://github.com/farooq-teqniqly/pr-center/issues/44).

## What Changes

- **A two-table diagnostics store.** A `PollRun` parent row per refresh and a
  `PollOwnerDiagnostic` child row per configured owner. The parent exists
  because two facts have nowhere else to live: the published item count -- the
  only place a cross-owner duplicate is visible, since each owner's own counts
  are individually correct -- and a poll that fails before any owner is
  enumerated, which under a rows-only schema would leave no trace at all.
- **Every poll accounts for every configured owner.** An owner never reached
  (the refresh aborted first) gets a row with status `NotPolled`, a null start
  instant, and null counts, so the abort point is visible as the boundary
  between polled and unpolled rows. Null counts and zero counts stay distinct:
  zero means GitHub returned nothing, null means it was never asked.
- **The poll's outcome is recorded, not just its owners.** `Succeeded`,
  `AbortedByLock`, `Canceled`, or `Faulted`. Without it, rows from an aborted
  poll read as describing what is on screen, when the UI is in fact still
  showing the previous snapshot -- exactly the confusion this table exists to
  end.
- **The write happens in a `finally` inside `RefreshQueue.ExecuteAsync`**, so a
  poll that aborts or faults still records what it saw. It uses its own bounded
  cancellation token, because on shutdown the caller's token is already canceled
  and a write on it would fail on the one path the `finally` exists for. A sink
  failure is logged at warning and swallowed; it can neither fail a poll nor
  replace the exception already in flight.
- **The `union -> derived` cliff is explained, not just measured.** A row
  carrying `union=15, derived=4` says nothing about the missing 11. The
  exclusion reasons already exist as `MembershipExclusion`, and
  `QueueItemDeriver.Derive` currently discards them by returning `QueueItem?`.
  It now returns a result carrying either the item or the exclusion, and the
  refresh counts by reason: `draft 6, closed 2, approved 3`.
- **Rate limit comes from the GraphQL response body, not the response
  headers.** GraphQL bills in points, not requests, so `x-ratelimit-*` reports a
  number that does not describe what the query cost. A `rateLimit { remaining
  resetAt cost }` field is added to the review-queue query document instead.
- **`OwnerFactsResult` grows a fetch-diagnostics companion** carrying the two
  per-search node counts and the rate-limit reading -- the only facts born
  inside the adapter and otherwise unobservable. The union count is not part of
  it: it is already `Facts.Count`.
- **A read surface.** A diagnostics port and a settings-page view of the last N
  polls, with a copy action. "Safe to paste into an issue" implies something to
  copy, and a table that cannot be seen from inside the container is a table
  that will not be trusted. The view is one grouped list -- owner rows collapsed
  under each poll, no navigation and no second read -- with the published count
  on every summary line, because the case that motivated this change is about
  what moved between polls, not about what one poll saw. A poll whose owners
  overlap is marked so its counts read correctly, without being dressed up as a
  fault: cross-owner overlap is routine and already handled.

## Capabilities

### New Capabilities

- `poll-diagnostics`: the per-poll and per-owner diagnostics record, its
  redaction rules, the sink fan-out, SQLite persistence with ring-buffer
  retention, and the read surface.

### Modified Capabilities

- `polling-and-refresh`: a refresh produces one diagnostics record per poll and
  writes it on every exit path, including abort, cancellation, and fault.
- `github-adapter`: `OwnerFactsResult` carries per-search node counts and a
  rate-limit reading on success; the review-queue query document requests
  `rateLimit`.
- `queue-derivation`: `QueueItemDeriver.Derive` returns a result carrying the
  exclusion reason for a hidden pull request instead of a bare null.

## Non-goals

- **Storing raw response bodies.** Payloads carry private-org PR titles, comment
  text, and reviewer logins. The app encrypts PATs at rest under an
  app-password-derived key; writing response bodies unencrypted into the same
  SQLite file would be a larger exposure than the secret already protected.
  Volume is the second objection: up to 100 PR nodes per response with reviews,
  comments, commits, and threads, times owners, times retained polls. If raw
  capture is ever needed it belongs behind an off-by-default verbose setting
  with a much smaller cap, as its own change.
- **Reading diagnostics from any derivation path.** Membership is derived each
  poll as a pure function of current GitHub facts; a deriver that read this
  table would reintroduce stored state and break that invariant. The sink
  interface has no read member, and nothing in `PrCenter.Core.Queue` references
  the reader port.
- **A configurable retention window.** N is a constant (200 polls, about 16
  hours at the 5-minute default). Promotable to an app setting later without a
  schema change.
- **A per-owner time series across polls.** No sparkline, no chart, no
  owner-selected history view. It would answer "when did this owner's number
  move?" directly rather than by scanning, at the cost of a second reader shape,
  an owner selector, and a rendering decision. The published count on each
  summary line already makes the across-poll read scannable, and the stored rows
  answer the sharper question from a SQLite client against the mounted volume.
- **OpenTelemetry.** Spans and metrics are `add-observability` (#9). This change
  only chooses field names that read as span attributes (`pr_center.poll.id`,
  `pr_center.owner`, `pr_center.search.requested.count`) so that mapping is
  mechanical.
- **Diagnostics for the single-PR fetch.** `GetPullRequestFactsAsync` is not
  part of a poll.

## Impact

- `PrCenter.Core`: the diagnostics record and its sub-records; an
  `IPollDiagnosticsSink` write-only port and an `IPollDiagnosticsReader` port;
  a `PollDiagnosticsAccumulator`; `RefreshQueue` gains the `finally` and a
  `RefreshPass` grouping of the three per-refresh collections;
  `QueueItemDeriver.Derive` returns a result type; `OwnerFactsResult` grows a
  fetch-diagnostics property.
- `PrCenter.GitHub`: `rateLimit` added to the review-queue query document and
  mapped; per-search node counts captured during the union.
- `PrCenter.Persistence`: `PollRun` and `PollOwnerDiagnostic` entities, their
  mapping and migration, the SQLite sink with transactional insert-plus-trim,
  and the reader.
- `PrCenter.Web`: sink fan-out registration and the last-N-polls view on the
  settings page.
- Tests: `PrCenter.Core.Tests` (record shape, exclusion counting, write-on-every-
  exit-path with a fake sink, sink failure isolation), `PrCenter.GitHub.Tests`
  (counts and rate-limit mapping), `PrCenter.Persistence.Tests` (round-trip,
  retention by poll, atomicity against a real SQLite file),
  `PrCenter.Web.Tests` (bUnit: the diagnostics view).
