# Design: add-poll-diagnostics

## Context

The facts a diagnostics row needs are split across two layers, and that split is
the whole design problem.

```
RefreshQueue.ExecuteAsync                    knows: owners, poll id, wall clock,
  |- per owner: RefreshOwnerAsync                   resolved login, derived count,
  |    |- GetAuthenticatedUserLoginAsync            status/detail, carry-over
  |    |- GetReviewQueueFactsAsync --+
  |    |    ClassifyAsync            |     GitHubFactsClient knows:
  |    |    UnionFacts --------------+       per-search node counts, rate limit
  |    |- QueueItemDeriver.Derive
  |- holder.Publish(DistinctPullRequests())   <-- cross-owner dedupe happens HERE
```

Current state this builds on:

- `OwnerFactsResult` (`src/PrCenter.Core/Ports/OwnerFactsResult.cs`) carries
  `Status`, `Facts`, `Detail` and nothing else.
- `GitHubFactsClient.UnionFacts` (`src/PrCenter.GitHub/GitHubFactsClient.cs:350`)
  walks the `requested` and `reviewed` aliases into one dictionary; the
  per-alias node counts exist only inside that loop.
- `RefreshQueue.ExecuteAsync` (`src/PrCenter.Core/Queue/RefreshQueue.cs:48`)
  wraps the owner loop in a `try` that catches only `VaultLockedException`;
  `_vault.ListOwnersAsync` sits outside that `try`.
- `RefreshOwnerAsync` catches everything except `VaultLockedException` and a
  caller-requested cancellation, so per-owner faults never escape.
- `QueueItemAccumulator` (`src/PrCenter.Core/Queue/QueueItemAccumulator.cs`) is
  the existing idiom for refresh-scoped machinery: an internal class fed from
  one call site, no null guards.
- `MembershipDeriver.Derive` already returns a `MembershipResult` carrying
  either a `MembershipState` or a `MembershipExclusion`.
  `QueueItemDeriver.Derive` (`src/PrCenter.Core/Derivation/QueueItemDeriver.cs:32`)
  consumes it and discards the exclusion by returning `QueueItem?`.
- `QueuePollingService.RunCycleAsync` wraps each poll in
  `await using var scope`, so any scoped `DbContext` is alive for the duration
  of `ExecuteAsync` and not one instruction longer.

## Goals / Non-Goals

**Goals:**

- One record per poll, produced once, fanned out to sinks.
- Written on every exit path, including the paths that produce no snapshot.
- Redacted by construction: identifiers, counts, and instants only.
- Field names that map mechanically to OpenTelemetry span attributes.
- Retention that evicts whole polls, never partial ones.

**Non-Goals:**

- Raw response bodies, a readable path from any deriver, a configurable N,
  OpenTelemetry itself, and single-PR-fetch diagnostics (all per the proposal's
  Non-goals).

## Decisions

### The adapter returns diagnostics; it does not write them

`OwnerFactsResult` grows an optional `FetchDiagnostics` property. The adapter
stays sink-ignorant and there remains exactly one producer of the record.

The alternative -- injecting a sink into `GitHubFactsClient` so it writes its
own row -- was rejected: two producers means two rows per owner per poll to
join, and it puts a persistence concern inside the GitHub adapter.

`FetchDiagnostics` carries three things:

```
FetchDiagnostics { RequestedCount, ReviewedCount, RateLimit }
```

Not the union count. That is already `OwnerFactsResult.Facts.Count`, so
`RefreshQueue` computes it for free; adding it to the port would be a second
source of truth for one number.

`FetchDiagnostics` is null on every failure path, because `Failure(...)`
constructs a result with no facts and no counts. That nullability is the signal:
a null count means the search never returned, not that it returned nothing.

### Rate limit comes from the query document, not the headers

GitHub bills GraphQL in points, not requests, so `x-ratelimit-remaining` on a
GraphQL response does not describe what this query cost. The accurate source is
a `rateLimit { remaining resetAt cost }` field in the query itself -- one line
in `GitHubGraphQlQueries.ReviewQueue`, mapped alongside the search aliases.
`cost` is kept as well: it is the number that predicts when the limit will bite.

### `Derive` returns a result, not a nullable

Mirroring `MembershipResult`, `QueueItemDeriver.Derive` returns a
`QueueItemResult` with `Shown(QueueItem)` / `Hidden(MembershipExclusion)`
factories. The refresh tallies the hidden results by reason.

This is a change to a pure Core type and its tests, taken deliberately: a row
reading `union=15, derived=4` poses the question it was supposed to answer,
while `union=15, derived=4 (draft 6, closed 2, approved 3)` answers it, and the
exclusion reasons are already computed one call down the stack. Discarding them
and re-deriving them for diagnostics would be the worse trade.

### Two tables, because two facts have nowhere else to live

```
PollRun (Id, PollId, StartedAt, CompletedAt, Outcome, ConfiguredOwners?,
         OwnerCount?, PublishedCount?)
   |
   +-- PollOwnerDiagnostic (PollRunId, Owner, ResolvedLogin, StartedAt,
         CompletedAt, Status, Detail, counts..., exclusions..., rate limit...,
         PullRequestIds)
```

`PublishedCount` is the cross-owner duplicate signal. Each owner's own
`DerivedCount` was individually correct in the #43 case -- two owners each
reported one item for the same pull request, both honestly. The duplicate is
only visible as `sum(DerivedCount) > PublishedCount`, or by intersecting the
`PullRequestIds` sets. Neither is a per-owner fact.

The second force is a poll that fails inside `ListOwnersAsync`: zero owners are
known, so a rows-only schema records nothing about the poll that failed hardest.

`ConfiguredOwners` is captured from `ListOwnersAsync` directly, not assembled
from the owner rows, and `OwnerCount` is derived from it. The duplication is
deliberate. The owner rows come out of `PollDiagnosticsAccumulator` -- the code a
reader consults this table to debug -- so a record whose only account of the
configured owners is those rows cannot expose a fault in producing them:

```
_vault.ListOwnersAsync() ──► ConfiguredOwners        (independent witness)
          │
          └─► RefreshOwnerAsync ──► accumulator ──► owner rows
                                        ▲
                              the machinery under suspicion
```

`rows != ConfiguredOwners` is then a detectable defect. Without the independent
capture it is invisible, because the invariant would be checked against itself.

`OwnerCount` is nullable for exactly that path. Zero is a real configuration --
`polling-and-refresh` already specifies that a refresh with no stored tokens
publishes an empty snapshot -- so writing zero when the owner list could not be
read would render a broken vault as an empty one on the summary line. Null means
"never enumerated", the same null-versus-zero rule the owner rows follow.

Paired with `PublishedCount` and the count of owner rows, this makes the summary
line answer "did anything go wrong?" without expanding the poll:

```
14:05:12   ok        3/3 owners   published 12
13:55:10   aborted   1/3 owners   published --
13:50:09   faulted   -/- owners   published --     <- owner list unreadable
```

The parent also makes retention a plain `DELETE` with a cascade rather than a
subquery over a denormalized column.

### `PollRun` has both an int key and a Guid

`Id` is an autoincrement int: it orders naturally for the trim and cascades to
children. `PollId` is a `Guid`, and it is what leaves the machine as the
`pr_center.poll.id` span attribute in #9 -- a local rowid is meaningless as a
trace correlator.

### `PullRequestIds` is a column, not a third table

A delimited `owner/repo#number` list on the owner row, via an EF value
converter. With a parent table already introduced, a third table for identifiers
is over-modeling; "which polls contained PR X" becomes a `LIKE`, which is fine
at ring-buffer scale. Titles and bodies are never stored -- the identifier is the
whole point and the whole content.

### Write in a `finally` inside `ExecuteAsync`, with its own token

Enumerating the ways a refresh can leave without reaching the end:

| Path | Escape | Recorded as |
|---|---|---|
| `ListOwnersAsync` throws | propagates (today it sits outside the `try`) | `Faulted`, zero owner rows |
| `VaultLockedException` | caught, returns `RefreshAbortedByLock` | `AbortedByLock` |
| shutdown cancellation | propagates from `RefreshOwnerAsync` | `Canceled` |
| `_holder.Publish` throws | propagates | `Faulted`, `PublishedCount` null |
| normal | -- | `Succeeded` |

Three consequences:

1. `ListOwnersAsync` moves inside the `try`, so its failure is recorded rather
   than escaping before the record exists.
2. The sink write uses its own bounded `CancellationTokenSource` (2 seconds),
   never the caller's. On shutdown the caller's token is already canceled, so a
   write on it fails immediately -- on the exact path the `finally` was built
   for. Bounded rather than `None` because the host shutdown timeout is the only
   thing between a blocked SQLite write and a killed container.
3. The write is wrapped in its own `try`/`catch` inside the `finally`, logging
   at warning. A throwing `finally` would replace the exception already in
   flight, so a diagnostics failure could disguise a real cancellation or fault.

The write stays inside `ExecuteAsync` rather than moving up into
`QueuePollingService`, because the per-wake DI scope -- and therefore the scoped
`DbContext` -- is disposed the moment `ExecuteAsync` returns. This is an
invariant, not an incidental: moving the write outward is a use-after-dispose.

Accepted consequence of end-of-poll writing: a process killed mid-poll (SIGKILL,
power loss) records nothing for that poll. Streaming per-owner writes would
survive that, at the cost of a separate trim pass and of a partial poll being
indistinguishable from a complete one. The failure mode chosen is the rarer one.

### Owners never reached get a row, not an absence

`Status` extends `OwnerFetchStatus` with `NotPolled` for owners the loop never
got to. Their `StartedAt` is null. Every poll therefore has exactly
`OwnerCount` child rows, and the abort point reads off the table directly:

```
PollId=7  perfectServe   Ok         requested=12 reviewed=8 union=15 derived=4
PollId=7  ps-unite       Ok         requested=3  reviewed=1 union=3  derived=1
PollId=7  farooq-...     NotPolled  -            -          -        -
```

A `NotPolled` row and a zero-count `Ok` row are different claims and must never
collapse into each other.

### `CarriedOverCount` is a first-class count

The issue's field list omits it, but for a failed owner it is the only count
with meaning: "5 rows carried from 13:55" versus "0 rows carried, this owner has
never been fresh" are different situations, and the fetch counts are null in
both.

### Field grouping

The record would be 15+ flat fields, over the 7-parameter limit, so it groups
into sub-records that name real concepts:

```
PollDiagnostics
  |- PollRun        { PollId, StartedAt, CompletedAt, Outcome,
                      ConfiguredOwners, PublishedCount }
  |- IReadOnlyList<OwnerPollDiagnostics>

OwnerPollDiagnostics
  |- OwnerPollWindow         { Owner, StartedAt, CompletedAt }
  |- OwnerOutcome            { Status, Detail, ResolvedLogin }
  |- FetchCounts             { Requested, Reviewed, Union, Derived, CarriedOver }
  |- ExclusionCounts         { Draft, ClosedOrMerged, Approved, Untracked }
  |- RateLimit               { Remaining, ResetAt, Cost }
  |- ContributedPullRequests { Ids, ForeignCount }
```

Six constructor parameters at the owner level, six and below inside each
sub-record.

`ContributedPullRequests` groups the identifiers with the count derived from
them rather than adding a seventh owner-level parameter. The grouping is a real
concept, not a parameter-budget dodge: the foreign count is meaningless apart
from the list it counts, and computing one without the other is a bug.

`OwnerCount` is not a field. It is `ConfiguredOwners?.Count`, so the two can
never disagree; only the persistence row stores it separately, as a column the
reader can order and filter on without deserializing the list.

### `RefreshPass` groups the three per-refresh collections

`RefreshOwnerAsync` currently threads `items` and `statuses` through its
signature; diagnostics would make three parallel collections and six parameters.
They are one concept -- the refresh in progress -- so they fold into a
refresh-scoped `RefreshPass` alongside the existing `QueueItemAccumulator`,
dropping the signature back to `(owner, previous, pass, cancellationToken)`.

This refactor is in scope rather than deferred because this change is what makes
the third collection appear.

### Sink fan-out and the read boundary

```
Core:        IPollDiagnosticsSink   { WriteAsync(PollDiagnostics, ct) }   write-only
             IPollDiagnosticsReader { GetRecentPollsAsync(int, ct) }
Persistence: SqlitePollDiagnosticsSink, SqlitePollDiagnosticsReader
Web (#9):    OpenTelemetryPollDiagnosticsSink
Fan-out:     IEnumerable<IPollDiagnosticsSink> injected into RefreshQueue
```

`RefreshQueue`'s constructor goes from 4 parameters to 6 (`+ sinks`,
`+ TimeProvider`), inside the limit.

The write/read split is the enforcement of "no derivation path reads this
table": the sink interface has no read member, and nothing under
`PrCenter.Core.Queue` or `PrCenter.Core.Derivation` references the reader. An
architecture test asserts the second half, since the compiler cannot.

### Retention

N = 200 polls, a constant. At the 5-minute default that is about 16 hours -- long
enough to cover "it looked wrong when I got in this morning".

Insert and trim run in one transaction:

```
INSERT PollRun; INSERT children;
DELETE FROM PollRun WHERE Id NOT IN (SELECT Id FROM PollRun ORDER BY Id DESC LIMIT 200);
```

Trimming by poll, not by row count, so a five-owner poll does not evict polls
unevenly. Children go with the cascade.

## Risks / Trade-offs

- **The tests get a new fake.** Every existing `RefreshQueue` test needs a sink.
  Mitigated by a no-op sink default in the test helper rather than by making the
  dependency optional in production wiring.
- **`Derive`'s signature change ripples.** `QueueItemDeriver` is internal and has
  one production caller, but its tests assert on `QueueItem?` throughout.
  Mechanical, and covered by the tasks.
- **Diagnostics volume in the container.** 200 polls times owners times an id
  list is kilobytes, not megabytes. No index beyond the primary keys and the FK
  is warranted at that size.
- **`Canceled` polls will be common.** Every container stop mid-poll writes one.
  That is intended -- a poll that never finished is exactly what a reader wants
  to know about -- but the read surface should not present it as a failure.

### The read surface groups by poll; it does not navigate

A drill-down would mean a route, a second reader member, a second round-trip,
and selection state. Grouping the flat list by poll with the owner rows
collapsed gives the same affordance for none of that:

```
v 14:05:12   ok        3 owners   published 12
    perfectServe  Ok  req 12 rev 8 union 15 derived 4 (draft 6, closed 2, approved 3)
    ps-unite      Ok  req 3  rev 1 union 3  derived 1
    farooq-...    Ok  req 5  rev 5 union 7  derived 7
> 14:00:11   ok        3 owners   published 13
> 13:55:10   aborted   1/3 owners published --
```

The reader therefore stays a single `GetRecentPollsAsync(count)` returning whole
graphs. At 200 polls times a handful of owners the payload is low hundreds of
kilobytes; splitting it into summaries plus a per-poll fetch would buy nothing
and cost a second port member.

### The collapsed line carries the published count, because change across polls
is the real question

The case that motivated this change -- a duplicate row that "took several
refresh cycles to appear" -- is not a question about one poll. It is a question
about what moved between polls. A list that answers "what did this poll see?"
perfectly still under-serves it, because nobody scanning sixty integers notices
`derived 4 -> 5`.

Putting the published count on the collapsed line makes one scannable column a
free time series -- it is already a stored `PollRun` field:

```
14:05:12   ok   published 12
14:00:11   ok   published 13   <- moved, and no pull request was opened
13:55:10   ok   published 12
```

### The foreign-item count names which token reaches across

The poll-level overlap tell says a pull request arrived from more than one owner.
It does not say which. Each identifier is already `owner/repo#number`, so
comparing its owner against the row's owner costs one pass over data the row
already holds:

```
perfectServe   derived 4   ids: perfectServe/api#12, perfectServe/api#19,
                                ps-unite/tools#3,              <- foreign
                                farooq-teqniqly/pr-center#42   <- foreign
                           foreign 2
```

Like the overlap tell, a non-zero count is normal: a fine-grained PAT whose
resource owner is one org can still see repositories in another configured
owner, and `QueueItemAccumulator` resolves the result correctly. The value is in
attribution -- it turns "two owners saw the same pull request" into "this token
is the one reaching across".

Stored rather than computed at read time so #9 can emit it as a counter without
re-parsing identifiers.

### A duplicate tell, framed as a marker and not an error

When a poll's owner rows sum to more derived items than the poll published, one
pull request reached the queue from more than one owner. That is exactly the
#43 signature, and it is one comparison over data already loaded.

It is deliberately *not* an error state. Cross-owner overlap is legitimate --
`QueueItemAccumulator` exists to resolve it, and a token that can see a second
configured owner's repositories produces it routinely. The marker means "the
counts below will not add up the way you expect, and here is why", so the
wording must not imply a fault. A tell that reads as an alarm on a healthy
install is a tell that gets ignored on a broken one.

### Query cost is stored but not rendered

`cost` is captured, persisted, and carried on the record; the diagnostics view
does not show it. Storing it is one integer column on a row that already exists,
and it is the number that predicts when the rate limit will bite -- but that is a
trend question, and this view answers per-poll questions. Rendering it would add
a column nobody reads per poll.

It is stored rather than deferred because the value cannot be recovered later:
#9 turns it into a metric, and a metric over polls that never recorded it starts
with a hole. Cheap to keep, impossible to backfill.

## Open Questions

None outstanding.
