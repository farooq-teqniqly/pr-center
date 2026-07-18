# Tasks: replace-marker-with-review-baseline

## 1. Update detector measures against my last review

- [ ] 1.1 In `PrCenter.Core.Tests/Derivation/UpdateDetectorTests.cs`, flip the
  null-baseline test: rename/rewrite "unseen when never looked at" to
  **null baseline yields no update** (assert `HasUpdate` returns false when the
  baseline is null, even with other people's activity present). Rename the
  remaining tests off "marker" onto "my last review". Run to confirm red.
- [ ] 1.2 In `src/PrCenter.Core/Derivation/UpdateDetector.cs`, rename the third
  parameter `lastSeen` to `myLastReviewedAt` and change the null branch to
  `return false` (an unreviewed pull request is new, not updated). Leave the
  strictly-after / not-me / bot-filter comparison unchanged. Update the
  `<summary>`, `<param>`, and the class comment to name the review baseline
  rather than the last-seen marker. Run tests green.

## 2. Queue item drops last-looked, derives its own baseline

- [ ] 2.1 In `PrCenter.Core.Tests/Derivation/QueueItemDeriverTests.cs`, update
  the deriver tests to the new `Derive(facts, myLogin)` signature (no marker
  argument), assert the item's has-update is computed against the user's latest
  review instant, and replace "never looked and never reviewed" with
  **never reviewed is explicit** (last-reviewed null). Run to confirm red.
- [ ] 2.2 In `src/PrCenter.Core/Derivation/MyEngagement.cs`, remove
  `LastLookedAt` (constructor param, property, and its docs); keep the one-field
  record carrying `LastReviewedAt` (D3 keeps the domain grouping).
- [ ] 2.3 In `src/PrCenter.Core/Derivation/QueueItemDeriver.cs`, drop the
  `lastSeen` parameter; compute the baseline once via the existing
  `LastReviewedByMe(facts, myLogin)` and pass that same instant both to
  `UpdateDetector.HasUpdate` and to the single-field `MyEngagement`. Update the
  `<param>`/`<summary>` docs. Run tests green.

## 3. Refresh stops touching the store

- [ ] 3.1 In `PrCenter.Core.Tests/Queue/RefreshQueueTests.cs`, remove the
  `IStateStore` substitute and any marker-read expectations; the refresh derives
  items from facts alone. Add/keep a case asserting a derived item's has-update
  reflects the review-derived baseline with no store interaction. Run to confirm
  red.
- [ ] 3.2 In `src/PrCenter.Core/Queue/RefreshQueue.cs`, remove the `IStateStore`
  field, constructor parameter, and its `<param>` doc; rewrite
  `DeriveItemAsync` to call `QueueItemDeriver.Derive(facts, myLogin)` with no
  store round-trip (drop the `GetLastSeenAsync` call). Update the class summary
  to drop "against the stored last-seen markers". Run tests green.

## 4. Remove the mark-as-seen use case

- [ ] 4.1 Delete `src/PrCenter.Core/Queue/MarkSeen.cs` and
  `PrCenter.Core.Tests/Queue/MarkSeenTests.cs`.
- [ ] 4.2 In `src/PrCenter.Web/Polling/QueueServiceCollectionExtensions.cs`,
  remove the `services.AddScoped<MarkSeen>();` registration and drop "mark-seen"
  from the method/type summary. Update
  `PrCenter.Web.Tests/DiCompositionRootTests.cs` to stop resolving/asserting
  `MarkSeen`.
- [ ] 4.3 Confirm no other caller uses `IGitHubFacts.GetPullRequestFactsAsync`
  (grep). If `MarkSeen` was its only caller, note it as now-unused; leave the
  port method in place unless removing it is trivially clean (out of this
  change's stated scope otherwise).

## 5. Delete the marker store and port

- [ ] 5.1 In `PrCenter.Persistence.Tests`, delete `StateStoreTests.cs`. Update
  `SqliteTestDatabaseTests.cs` / `PersistenceMigrationExtensionsTests.cs` if any
  assert the `LastSeenMarkers` table or marker round-trip; re-point the harness
  proof at the token-vault schema (per the token-vault spec's real-file
  scenario).
- [ ] 5.2 Delete `src/PrCenter.Persistence/StateStore.cs`,
  `StateStore.Logging.cs`, and `LastSeenMarker.cs`.
- [ ] 5.3 Delete `src/PrCenter.Core/Ports/IStateStore.cs` (its whole surface is
  last-seen). Remove the `IStateStore` registration and its mention in the
  `AddPersistenceAdapter` summary in
  `src/PrCenter.Persistence/PersistenceServiceCollectionExtensions.cs`.
- [ ] 5.4 In `src/PrCenter.Persistence/PrCenterDbContext.cs`, remove the
  `LastSeenMarkers` `DbSet` and its `OnModelCreating` entity block; update the
  class summary to drop "last-seen markers".

## 6. Drop the marker table via migration

- [ ] 6.1 With the `LastSeenMarker` entity already removed from the model
  (section 5), generate a forward migration that drops the table:
  `dotnet ef migrations add DropLastSeenMarkers -p src/PrCenter.Persistence`.
  Verify the generated `Up` drops `LastSeenMarkers` and `Down` recreates it, and
  that the model snapshot no longer contains the entity.
- [ ] 6.2 Apply migrations against a scratch SQLite file and confirm the table is
  gone and the token/security tables are untouched (no data migration needed).

## 7. token-vault owns the persistence foundation (spec bookkeeping)

- [ ] 7.1 Confirm the foundation behavior the token-vault spec now asserts
  (startup migration, WAL/busy/command-timeout via `SqliteContextConfiguration`
  + `SqlitePragmaInterceptor`, Development-only diagnostics, real-file
  integration harness) still holds after sections 5-6 -- no code changes
  expected, existing `TokenVaultTests` / `SqliteTestDatabaseTests` cover it.

## 8. Docs sweep

- [ ] 8.1 Rewrite `docs/pr-center-state.md` section 2 to the review-derived
  update machine (baseline = my latest review; null baseline = no update; no
  stored marker).
- [ ] 8.2 Correct `docs/pr-center-roadmap.md` #3/#5 scope and confirm this change
  is recorded ahead of #6 (per the proposal's docs-swept note).

## 9. Verify green

- [ ] 9.1 Run `dotnet csharpier check`, then `dotnet build` (warnings-as-errors)
  and the full `dotnet test` suite; all green.
- [ ] 9.2 Collect coverage per changed test project via the coverlet collector
  and confirm the changed Core/Persistence classes stay covered (null-baseline
  branch exercised); `rm -rf` the `TestResults/` artifact after reading.
- [ ] 9.3 Run `openspec validate replace-marker-with-review-baseline --strict`.
