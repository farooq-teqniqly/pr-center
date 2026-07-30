# Tasks: add-poll-diagnostics

Each group is red-green-refactor: the test task precedes the implementation task
it drives, and the implementation task is done when that test passes.

## 1. Derivation reports why a pull request is hidden

- [x] 1.1 Write failing `QueueItemDeriverTests` changes: a shown pull request yields a shown result carrying the `QueueItem`; a draft, a closed/merged, an approved-and-unrequested, and a never-requested-never-reviewed pull request each yield a hidden result carrying the matching `MembershipExclusion`; the existing null/whitespace guard tests still hold.
- [x] 1.2 Add `PrCenter.Core/Derivation/QueueItemResult.cs` -- `sealed record` mirroring `MembershipResult`: private constructor, `IsShown`, nullable `Item`, nullable `Exclusion`, and `Shown(QueueItem)` / `Hidden(MembershipExclusion)` factories. XML docs on the type and every member.
- [x] 1.3 Change `QueueItemDeriver.Derive` to return `QueueItemResult`, passing through the exclusion `MembershipDeriver` already produced. Update its XML docs.
- [x] 1.4 Update `RefreshQueue.RefreshOwnerAsync` to the new return type; behavior is unchanged at this step (hidden results are still simply not added).

## 2. Fetch diagnostics on the GitHub port

- [x] 2.1 Add `PrCenter.Core/Ports/RateLimitReading.cs` (`Remaining`, `ResetAt`, `Cost`) and `PrCenter.Core/Ports/FetchDiagnostics.cs` (`RequestedCount`, `ReviewedCount`, `RateLimit`), both `sealed record`, XML-documented. Document that the union count is deliberately absent -- it is `OwnerFactsResult.Facts.Count`.
- [x] 2.2 Add an optional `FetchDiagnostics?` property to `OwnerFactsResult`, defaulted to null so every failure path keeps its current construction. Document that null means "never asked", not "returned nothing".
- [x] 2.3 Write failing `GitHubFactsClientTests`: a successful fetch reports the node count of each search alias before deduplication; a pull request matching both aliases is counted in both while appearing once in the facts; each non-`Ok` status yields a null `FetchDiagnostics`.
- [x] 2.4 Capture the per-alias node counts in `GitHubFactsClient.UnionFacts` and thread them into the `Ok` result.
- [x] 2.5 Write failing tests for the rate limit: a response carrying `rateLimit` maps remaining, reset instant, and cost; a successful response omitting or malforming `rateLimit` still returns `Ok` with facts and a null rate-limit reading.
- [x] 2.6 Add `rateLimit { remaining resetAt cost }` to `GitHubGraphQlQueries.ReviewQueue` and map it in `GitHubFactsClient`. Do not read `x-ratelimit-*` headers.

## 3. The diagnostics record (Core)

- [x] 3.1 Add the record types under `PrCenter.Core/Diagnostics/`, each `sealed record`, null-guarded, XML-documented, and each within the 7-parameter limit: `PollDiagnostics` (poll part + owner rows), `PollRunDiagnostics` (`PollId`, `StartedAt`, `CompletedAt`, `Outcome`, nullable `ConfiguredOwners`, nullable `PublishedCount`), `OwnerPollDiagnostics` (window, outcome, fetch counts, exclusion counts, rate limit, contributed pull requests), `OwnerPollWindow`, `OwnerPollOutcome`, `FetchCounts`, `ExclusionCounts`, `ContributedPullRequests` (`Ids`, `ForeignCount`) -- the last groups the identifiers with the count derived from them so `OwnerPollDiagnostics` stays at six parameters. Document on `ConfiguredOwners` that it is captured from the owner enumeration and never assembled from the owner rows, that null means the enumeration never completed, and that an empty list means no owners are configured. The record exposes no separate owner count -- it is the list's length.
- [x] 3.2 Add `PollOutcome` (`Succeeded`, `AbortedByLock`, `Canceled`, `Faulted`) and extend `OwnerFetchStatus` with `NotPolled`. Document that `NotPolled` means the refresh never reached this owner, and is not a fetch failure.
- [x] 3.3 Add `PrCenter.Core/Ports/IPollDiagnosticsSink.cs` -- one `WriteAsync(PollDiagnostics, CancellationToken)` member and no read member. Document that the absence of a read member is the enforcement of the no-derivation-reads invariant.
- [x] 3.4 Add `PrCenter.Core/Ports/IPollDiagnosticsReader.cs` -- `GetRecentPollsAsync(int count, CancellationToken)`.
- [x] 3.5 Write failing `PollDiagnosticsAccumulatorTests`: the configured owners are those handed in at construction, not those recorded afterwards, so an owner never recorded still appears in the list; an owner's foreign-item count is the number of its identifiers whose owner differs from the row's owner, and zero when every identifier is its own; recording a polled owner captures its counts; recording a carried-over owner captures the carry-over count with null fetch counts; marking the remaining owners unreached produces `NotPolled` rows with null start instants; the built record always has one owner row per configured owner; exclusion counts plus the derived count equal the union count.
- [x] 3.6 Add `PrCenter.Core/Queue/PollDiagnosticsAccumulator.cs` -- refresh-scoped machinery in the shape of `QueueItemAccumulator` (internal, fed from one call site, no null guards).

## 4. RefreshQueue produces and writes the record

- [x] 4.1 Write failing `RefreshQueueTests` for the happy path: a successful refresh writes exactly one record, with one owner row per configured owner, the published count equal to the snapshot's item count, and per-owner requested/reviewed/union/derived counts.
- [x] 4.2 Write failing tests for cross-owner deduplication: when two owners return the same pull request, both owner rows record it in their identifiers and their derived counts sum higher than the record's published count.
- [x] 4.3 Write failing tests for the exit paths: a vault lock mid-refresh writes an `AbortedByLock` record with `NotPolled` rows for the owners never reached and no published count; a shutdown cancellation writes a `Canceled` record; a throwing `ListOwnersAsync` writes a `Faulted` record with absent configured owners, a null owner count, and no owner rows; a refresh over no stored tokens writes a record with an owner count of zero and no owner rows; a throwing `Publish` writes a `Faulted` record with every owner row and no published count.
- [x] 4.4 Write failing tests for sink isolation: a throwing sink leaves a successful refresh successful and logs a warning; with two sinks, the first throwing does not prevent the second; a throwing sink does not replace the exception propagating from a faulting refresh.
- [x] 4.5 Write a failing test that the sink write is not made with the caller's token: a refresh canceled by an already-canceled token still writes.
- [x] 4.6 Extract `PrCenter.Core/Queue/RefreshPass.cs` grouping the queue-item accumulator, the owner statuses, and the diagnostics accumulator; reduce `RefreshOwnerAsync` to `(owner, previous, pass, cancellationToken)`.
- [x] 4.7 Inject `IEnumerable<IPollDiagnosticsSink>` and `TimeProvider` into `RefreshQueue` (constructor goes to 6 parameters). Move `ListOwnersAsync` inside the `try`. Add the `finally` that marks unreached owners, builds the record, and writes it through every sink under its own 2-second `CancellationTokenSource`, with a `try`/`catch` per sink logging at warning.
- [x] 4.8 Add the warning `[LoggerMessage]` declarations to `RefreshQueue.Logging.cs`.
- [x] 4.9 Update the `RefreshQueue` XML summary to state that diagnostics are written on every exit path and are never read.

## 5. SQLite persistence

- [x] 5.1 Add `PrCenter.Persistence/PollRun.cs` (`Id` autoincrement, `PollId` Guid unique, `StartedAt`, `CompletedAt`, `Outcome`, nullable `ConfiguredOwners`, nullable `OwnerCount`, nullable `PublishedCount`) and `PrCenter.Persistence/PollOwnerDiagnostic.cs` (FK to `PollRun`, owner, resolved login, instants, status, detail, nullable counts, nullable exclusion counts, nullable rate-limit fields, `PullRequestIds`, `ForeignItemCount`). XML docs stating why each count is nullable.
- [x] 5.2 Map both in `PrCenterDbContext.OnModelCreating` with a cascade delete from `PollRun` to its children, and value converters for `PullRequestIds` and `ConfiguredOwners`. Add the `DbSet`s and update the context summary.
- [x] 5.3 Generate the EF migration (`AddPollDiagnostics`); confirm the `Up` is purely additive and touches no token or security column.
- [x] 5.4 Write failing `SqlitePollDiagnosticsSinkTests` against a real temporary SQLite file: a record round-trips with every field; `NotPolled` rows persist with null start instants and null counts; null counts and zero counts read back distinctly; identifiers and configured owners round-trip through their converters; foreign-item counts round-trip; a record with no owner rows persists.
- [x] 5.5 Write failing retention tests: writing while N polls are stored evicts exactly the oldest poll and all of its owner rows; eviction is by poll and not weighted by owner count; insert and eviction are one transaction, so a failure mid-write leaves neither the new poll nor a partial eviction.
- [x] 5.6 Implement `PrCenter.Persistence/SqlitePollDiagnosticsSink.cs` with the retention constant (200), insert-plus-trim in one transaction, and its `.Logging.cs` partial.
- [x] 5.7 Write failing `SqlitePollDiagnosticsReaderTests`: the most recent polls come back newest first, capped at the requested count, each with its owner rows; an empty table returns an empty list.
- [x] 5.8 Implement `PrCenter.Persistence/SqlitePollDiagnosticsReader.cs` as `AsNoTracking` projections of only the columns the view renders.
- [x] 5.9 Register the sink and the reader in `PersistenceServiceCollectionExtensions`.

## 6. Read surface (Web)

- [x] 6.1 Write failing bUnit tests for the list: polls render newest first, each summary line carrying instant, outcome, polled-owner ratio, and published count; a poll that polled fewer owners than configured shows the shortfall in the ratio without being expanded; a poll with a null owner count renders the ratio as unknown rather than as zero out of zero; owner rows are collapsed on first render; expanding a poll reveals its owner rows and issues no further read (assert against a reader called exactly once); a `Canceled` poll renders as incomplete rather than failed; a `NotPolled` owner row renders as not polled rather than as zero; an empty store renders an explicit empty state distinguishable from a read failure.
- [x] 6.2 Write failing bUnit tests for the owner-disagreement indication: a poll with a configured owner that has no row is surfaced as disagreeing and names that owner; a poll with a row for an unconfigured owner likewise; a consistent poll carries no indication; a poll with absent configured owners is not surfaced as disagreeing. Unlike the overlap mark, this one reads as a defect.
- [x] 6.3 Write failing bUnit tests for the overlap mark: a poll whose owner rows sum to more derived items than it published is marked and shows both totals; a poll with no overlap carries no mark; a marked poll with a successful outcome still renders as successful, with the mark styled as neither an error nor a warning.
- [x] 6.4 Write a failing bUnit test for the copy action: it produces text carrying identifiers, counts, statuses, and instants, and no pull request title or other GitHub payload.
- [x] 6.5 Add the diagnostics view to the settings page, behind the same unlock gating as the rest of the screen: a grouped list over a single `GetRecentPollsAsync` call, collapsed owner rows, the owner-disagreement indication, the overlap mark, the per-owner foreign-item count, and the copy action. Cap the rendered polls at 20 while leaving the reader able to return more.
- [x] 6.6 Verify the rendered and copied output against the redaction rule by inspection: identifiers, counts, instants, and system-composed details only.

## 7. Invariant and cleanup

- [ ] 7.1 Write a failing architecture test asserting no type under `PrCenter.Core.Queue` or `PrCenter.Core.Derivation` references `IPollDiagnosticsReader`, and that `IPollDiagnosticsSink` declares no read member.
- [ ] 7.2 Run the full solution build and every test project; collect coverage per `CLAUDE.md` and confirm the new Core and Persistence types are covered, then delete `TestResults/`.
- [ ] 7.3 Update `docs/pr-center-roadmap.md` item 9 to point at the shipped record rather than a planned one, and add the diagnostics table to `docs/pr-center-architecture.md` if it names the persistence schema.
- [ ] 7.4 Stop and wait for explicit approval before committing.
