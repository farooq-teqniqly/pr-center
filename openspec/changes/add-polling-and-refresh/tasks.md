## 1. Vault owner enumeration (D1)

- [x] 1.1 Red: `TokenVault` tests for `ListOwnersAsync` -- owners with tokens listed, empty vault lists none, works while locked without decryption (spec: token-vault delta)
- [x] 1.2 Green: add `ListOwnersAsync` to `ITokenVault` (XML docs) and implement in `TokenVault` as an `AsNoTracking` `Select(t => t.Owner)` projection

## 2. Queue snapshot and its observation (D5)

- [x] 2.1 Red: tests for the snapshot holder and `GetQueue` -- never-polled state before any publish, latest snapshot after publish, atomic replacement (readers see old or new in full)
- [x] 2.2 Green: `QueueSnapshot` (items, per-owner statuses, snapshot instant) plus a singleton holder with atomic reference swap; `GetQueue` use case reads it; snapshot instant from `TimeProvider`

## 3. RefreshQueue use case (D4-D7)

- [x] 3.1 Red: multi-owner refresh test -- enumerates owners via vault, resolves login per owner, fetches facts, derives items against markers, publishes snapshot with ok statuses
- [x] 3.2 Red: per-owner failure isolation test -- one failing owner degrades only its status, others' items still published
- [x] 3.3 Red: no-owners test -- empty snapshot published, `IGitHubFacts` never called
- [x] 3.4 Red: mid-poll `VaultLockedException` test -- refresh aborts, warning logged, previously published snapshot untouched
- [x] 3.5 Green: implement `RefreshQueue` (ports via constructor injection, no `IServiceProvider`; `[LoggerMessage]` logging split into a `.Logging.cs` partial)

## 4. MarkSeen use case (D2)

- [x] 4.1 Red: marker-from-facts test -- marker equals max timestamp across fetched commits/comments/reviews, including own and bot events
- [x] 4.2 Red: null-fetch test -- no marker written, no exception surfaces
- [x] 4.3 Red: empty-activity fallback test -- marker falls back to the facts' last-touch stamp
- [x] 4.4 Green: implement `MarkSeen` (live fetch via `IGitHubFacts.GetPullRequestFactsAsync`, then `IStateStore.SetLastSeenAsync`); extract the high-water-mark helper so the caller reads as intent

## 5. Refresh trigger and UnlockApp (D3)

- [x] 5.1 Red: trigger tests -- poke wakes a waiting consumer, pokes coalesce (capacity-1 drop-write), poke with no consumer pending is not lost
- [x] 5.2 Green: `IRefreshTrigger` port plus channel-backed implementation
- [x] 5.3 Red: `UnlockApp` tests -- successful unlock pokes the trigger, failed unlock does not, unlock result passes through
- [x] 5.4 Green: implement `UnlockApp` wrapping `IAppLock.UnlockAsync`

## 6. Polling BackgroundService (D4, D6)

- [ ] 6.1 Red: loop tests -- interval tick refreshes while Unlocked; wake while Locked/Uninitialized skips without touching the snapshot; trigger poke refreshes promptly; polls never overlap
- [ ] 6.2 Green: implement the polling `BackgroundService` in `PrCenter.Web` -- `PeriodicTimer` plus trigger await, lock check per wake, DI scope created per wake to resolve `RefreshQueue`
- [ ] 6.3 Green: poll-interval options bound from `appsettings.json` (default 5 minutes); test the configured-interval scenario

## 7. Composition root and wiring

- [ ] 7.1 Register the use cases, snapshot holder, trigger, `TimeProvider`, options, and hosted service in `Program.cs` / service-collection extensions
- [ ] 7.2 Verify architecture tests still pass (Core stays free of GitHub/EF/ASP.NET references; adapters do not reference each other)

## 8. Verification and closeout

- [ ] 8.1 Full solution build with warnings as errors, CSharpier check, all tests green
- [ ] 8.2 Coverage pass per testing convention (coverlet collector + Cobertura grep); no authored-code holes in the new use cases
- [ ] 8.3 Sweep docs if implementation diverged from design (design.md, architecture doc use-case list)
