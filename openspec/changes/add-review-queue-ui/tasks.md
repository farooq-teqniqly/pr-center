## 1. Core: snapshot change notification

- [x] 1.1 Write a failing `PrCenter.Core.Tests` test: `QueueSnapshotHolder.Publish` raises `Changed` after the reference swap, and a subscriber reading `Current` in the handler sees the just-published snapshot
- [x] 1.2 Write a failing test: `Publish` with no subscribers completes normally and the snapshot is readable afterward
- [x] 1.3 Add `event EventHandler? Changed` to `QueueSnapshotHolder`, raised inside `Publish` after the `Volatile.Write`; run tests green
- [x] 1.4 Refactor: null-safe raise, XML doc on the event; confirm the single raise site invariant

## 2. Web project and DI wiring

- [ ] 2.1 Confirm `PrCenter.Web` and `PrCenter.Web.Tests` (bUnit + NSubstitute) exist and are registered in the solution; add `bunit.web` / `TestAuthorizationContext` support if missing
- [ ] 2.2 Register DI for `GetQueue`, `UnlockApp`, `IAppLock`, `ITokenVault`, `IRefreshTrigger`, and `QueueSnapshotHolder` in the Web composition root (no `MarkSeen` -- it no longer exists)
- [ ] 2.3 Confirm Bootstrap 5.3.3 and Inter are loaded in `App.razor` (already are); use Bootstrap for layout/cards/badges/buttons/utilities and Inter for type
- [ ] 2.4 Add the tiny auto-dark script to `App.razor`'s `<head>`: set `document.documentElement`'s `data-bs-theme` from `matchMedia('(prefers-color-scheme: dark)')` at load
- [ ] 2.5 Port the mockup's semantic state tokens into `wwwroot/app.css` as custom variables layered over Bootstrap: `:root` light set plus a `[data-bs-theme="dark"]` override block (replacing the mockup's `@media` query and its `data-theme` hook)

## 3. Lock gate and unlock card

- [ ] 3.1 Write failing bUnit tests for `LockGate`: Unlocked renders the inbox, Locked renders the unlock card, Uninitialized renders the settings placeholder (mock `IAppLock`)
- [ ] 3.2 Implement `LockGate` reading `IAppLock.GetStateAsync`, re-evaluating on unlock and reset, no polling; run green
- [ ] 3.3 Write failing bUnit tests for `UnlockCard`: correct password re-evaluates to inbox, wrong password shows the message and stays, reset clears tokens to Uninitialized
- [ ] 3.4 Implement `UnlockCard` calling `UnlockApp.UnlockAsync` and `ITokenVault.ResetVaultAsync`; run green
- [ ] 3.5 Implement the Uninitialized settings placeholder (routes toward #7, builds nothing)
- [ ] 3.6 Add scoped `.razor.css` for the gate/unlock screens from the token block

## 4. Inbox: grouping, sort, and freshness

- [ ] 4.1 Write failing bUnit tests for `Inbox` grouping/sort: items grouped by owner then repository; within a group `HasUpdate` desc then `LastUpdate.At` desc; group order follows owner order and is stable across two snapshots
- [ ] 4.2 Implement the grouping/sort projection as a pure helper over the snapshot, user-relative; run green
- [ ] 4.3 Write failing test: `Inbox` subscribes to `QueueSnapshotHolder.Changed` in `OnInitialized`, re-renders from the new snapshot via `InvokeAsync(StateHasChanged)`, and unsubscribes on `Dispose`
- [ ] 4.4 Implement the holder subscription and `IDisposable`; no UI timer; run green
- [ ] 4.5 Write failing test + implement the manual refresh action poking `IRefreshTrigger` (no direct GitHub call)

## 5. Queue row and roster chips

- [ ] 5.1 Write failing bUnit tests for `QueueRow`: updated PR shows amber stripe + "Updated" badge; never-reviewed PR shows no badge but still renders; byline is `{LastUpdate.By} . relative(At)`; covered decoration names `CoveredBy`; the two instants (`LastReviewedAt`, `LastUpdate.At`) render
- [ ] 5.2 Implement `QueueRow` with the relative-time UI helper; run green
- [ ] 5.3 Write failing bUnit tests for `RosterChips`: chip color by `ReviewerState`, dashed "me" ring for `IsMe`, bot treatment for `IsBot`, and text labels so state is never color-only
- [ ] 5.4 Implement `RosterChips`; run green
- [ ] 5.5 Write failing test: the title is a plain `target="_blank"` anchor to `Identity.Url` with no side effect (no dispatch, no live fetch, no mark-seen)
- [ ] 5.6 Implement the plain-anchor title; run green
- [ ] 5.7 Add scoped `.razor.css` for row and chips from the token block

## 6. Owner status, banner, and empty states

- [ ] 6.1 Write failing bUnit tests for `OwnerChips` / `ErrorBanner`: all-ok shows ok chips and no banner; a non-`Ok` owner raises a labeled banner and a "stale {LastFreshAt}" chip while its carried rows still render
- [ ] 6.2 Implement `OwnerChips` and `ErrorBanner`; run green
- [ ] 6.3 Write failing bUnit tests: polled-and-empty shows "all caught up" with owner chips visible; null snapshot shows the distinct "polling has not run yet" state
- [ ] 6.4 Implement `EmptyState` and `NeverPolled`; run green
- [ ] 6.5 Add scoped `.razor.css` for chips, banner, and empty states

## 7. Verification

- [ ] 7.1 Run the full solution build and `csharpier check`
- [ ] 7.2 Run `PrCenter.Web.Tests` and `PrCenter.Core.Tests`; collect coverage per the CLAUDE.md coverlet flow and confirm the new components are exercised
- [ ] 7.3 Manually drive the app (locked -> unlock -> inbox, empty, and a non-ok owner) to confirm the four mockup screens render and re-render on publish without a timer
