# Tasks: add-settings-and-onboarding

Each group is red-green-refactor: the test task precedes the implementation task
it drives, and the implementation task is done when that test passes.

## 1. Poll interval value object (Core)

- [x] 1.1 Write failing `PollIntervalTests`: in-range construction round-trips the `TimeSpan`; below 5 minutes and above 24 hours throw; the boundaries (exactly 5 minutes, exactly 24 hours) are accepted; `Clamp` returns `Min` for a low value, `Max` for a high value, and the input unchanged for an in-range one.
- [x] 1.2 Add `PrCenter.Core/Settings/PollInterval.cs` -- `readonly record struct` over a `TimeSpan` with `Min` (5 min), `Max` (24 h), a range-checking constructor, and `static Clamp(TimeSpan)`. XML docs on the type and every member.

## 2. Settings port and adapter (Core + Persistence)

- [x] 2.1 Add `PrCenter.Core/Ports/IAppSettingsStore.cs`: `GetPollIntervalAsync` returning `PollInterval`, `SetPollIntervalAsync(PollInterval, ...)`. Document that reads and writes require no vault key and work in every lock state, and that an absent row reads as the 5-minute default.
- [x] 2.2 Add `PrCenter.Persistence/AppSetting.cs` -- singleton row (`Id` always 1, `ValueGeneratedNever`) with a `long PollIntervalSeconds` column -- and map it in `PrCenterDbContext.OnModelCreating`; add the `AppSettings` `DbSet` and update the context's XML summary, which currently says settings schema arrives later.
- [x] 2.3 Add nullable `DateTimeOffset? SavedAt` to `PrCenter.Persistence/OwnerToken.cs` and map it as optional.
- [x] 2.4 Generate one EF migration covering both schema changes (`AddSettingsAndTokenSavedAt`); confirm the generated `Up` is additive and does not rewrite `OwnerToken` secret columns.
- [x] 2.5 Write failing `AppSettingsStoreTests` against a real temporary SQLite file (existing integration harness): absent row reads the 5-minute default and creates no row; an in-range write round-trips; a second write replaces rather than inserting; an out-of-range stored value reads back clamped with a warning logged; a read succeeds while the vault is locked.
- [x] 2.6 Implement `PrCenter.Persistence/AppSettingsStore.cs` plus its `.Logging.cs` partial for the clamp warning. Reads are `AsNoTracking` projections; the upsert tracks.

## 3. Token deletion and saved-at (Persistence)

- [x] 3.1 Add `DeleteTokenAsync(string owner, ...)` to `ITokenVault` with XML docs naming the `VaultLockedException` and `ArgumentException` contracts.
- [x] 3.2 Extend `TokenVaultTests` (failing first) for deletion: deleting an owner with a token removes it from `ListOwnersAsync`; other owners' tokens and the security row survive; deleting an unknown owner succeeds silently; deleting while `Locked` and while `Uninitialized` throws `VaultLockedException` and removes nothing; a null/whitespace owner throws.
- [x] 3.3 Implement `TokenVault.DeleteTokenAsync` -- unlock gate first, then a single-row delete.
- [x] 3.4 Extend `TokenVaultTests` (failing first) for `SavedAt`: a store records the instant from the injected `TimeProvider`; a replace updates it; a row written without one reads back null.
- [x] 3.5 Inject `TimeProvider` into `TokenVault` and set `SavedAt` on every store path.
- [x] 3.6 Add a `ListOwnerTokensAsync`-style read returning owner plus `SavedAt` (no ciphertext, no decryption) on `ITokenVault`, with its own failing test first -- the settings table needs the instant and must not decrypt to get it.

## 4. Core use cases

- [x] 4.1 Write failing `InitializeVaultTests`: success sets the password, unlocks, and pokes the trigger; a failure from `SetPasswordAsync` propagates and pokes nothing; an unlock that returns false leaves the app locked and pokes nothing; null/whitespace password throws.
- [x] 4.2 Implement `PrCenter.Core/Settings/InitializeVault.cs` composing `ITokenVault.SetPasswordAsync`, `IAppLock.UnlockAsync`, and `IRefreshTrigger`.
- [x] 4.3 Write failing `SaveOwnerTokenTests` and `RemoveOwnerTests`: each delegates to the vault and pokes the trigger on success; a throwing vault call does not poke; guard tests for null/whitespace arguments.
- [x] 4.4 Implement `SaveOwnerToken` and `RemoveOwner`.
- [x] 4.5 Write failing `SavePollIntervalTests`: an in-range interval is written and the trigger poked; a store failure does not poke.
- [x] 4.6 Implement `SavePollInterval` over `IAppSettingsStore` + `IRefreshTrigger`.

## 5. Poll loop reads the stored interval

- [x] 5.1 Write failing `QueuePollingServiceTests` with a fake `TimeProvider`: the first arm uses the stored interval; after a wake the timer re-arms with the value read in that cycle, so an interval changed mid-run is honored on the next wake; a poke from the trigger also re-arms (interval means time since last poll); no stored row uses the 5-minute default.
- [x] 5.2 Rework `QueuePollingService`: drop `IOptions<PollingOptions>`, read `IAppSettingsStore` inside each wake's DI scope, and make the `ITimer` one-shot re-armed after every wake. Keep the single-trigger/no-overlap model intact.
- [x] 5.3 Delete `Polling/PollingOptions.cs`, `Polling/PollingOptionsValidator.cs`, their tests, and the `Polling` section in `appsettings.json`; drop the options registration and `ValidateOnStart` from `AddQueueServices`.
- [x] 5.4 Register `IAppSettingsStore` and the four new use cases in the DI extensions (scoped for the store and the use cases, matching `UnlockApp`).

## 6. Lock gate generalization and the settings route

- [x] 6.1 Write failing bUnit tests for `LockGate`: supplied `Locked`/`Uninitialized` fragments render for those states; when unsupplied, the unlock card and placeholder still render (the inbox regression check); `Unlocked` renders `ChildContent` either way.
- [x] 6.2 Add optional `Locked` and `Uninitialized` `RenderFragment` parameters to `LockGate.razor`, defaulting to the current components. `Inbox.razor` stays untouched.
- [x] 6.3 Write failing bUnit tests for the `/settings` page's three gated views: Uninitialized renders only the setup card; Locked renders the unlock-first message and link with no token, interval, or reset control; Unlocked renders the tokens table and interval control.
- [x] 6.4 Add `Components/Pages/Settings.razor` at `/settings` wrapping its three views in `LockGate`, and a nav entry in `NavMenu.razor`.
- [x] 6.5 Point `UninitializedPlaceholder.razor` at `/settings` with a real link; assert the link target in its test.

## 7. First-run setup card

- [x] 7.1 Write failing bUnit `SetupCardTests`: a valid password and matching confirmation calls `InitializeVault` once and raises the completed callback; under 8, over 32, and mismatched confirmation each show a message and call nothing; an in-range password with no digits or symbols is accepted.
- [x] 7.2 Implement the setup card component: password + confirm fields, the 8-32 rule, the strength suggestion text, and the `InitializeVault` call.
- [x] 7.3 Verify the settings page re-evaluates lock state after setup completes and lands on the Unlocked view (bUnit).

## 8. Tokens table and owner editing

- [x] 8.1 Write failing bUnit `OwnerTokensTests`: rows render owner, saved instant, and the status from the published snapshot; a null saved instant renders an explicit unknown; an owner absent from the snapshot's statuses renders not-yet-polled, not a failure; a non-ok status renders its detail; no token value appears anywhere in the markup; `LastFreshAt` is not rendered.
- [x] 8.2 Implement the tokens table over the owner-plus-`SavedAt` read and `QueueSnapshotHolder`'s current snapshot.
- [x] 8.3 Write failing bUnit tests for the add/replace/delete controls: a valid submission calls `SaveOwnerToken`; delete calls `RemoveOwner`; empty or whitespace owner or token, owner over 255 characters, and token over 512 characters each show a message and call nothing; no GitHub port is touched on any path.
- [x] 8.4 Implement the add/replace/delete controls with the shape-only validation from decision 12.

## 9. Poll interval control

- [x] 9.1 Write failing bUnit `PollIntervalControlTests`: the current interval and the allowed range render; an in-range save calls `SavePollInterval` once; a below-minimum and an above-maximum entry each show the range message and call nothing.
- [x] 9.2 Implement the interval control, range-checking input before constructing a `PollInterval`.

## 10. Reset confirmation on the unlock card

- [x] 10.1 Write failing bUnit tests on `UnlockCard`: invoking reset shows the confirmation step naming both the app password and the tokens, and calls nothing yet; a mismatched word calls nothing and stays on the confirmation step; cancelling calls nothing and returns to the unlock state; the exact word calls `ResetVaultAsync` once and raises `OnReset`.
- [x] 10.2 Implement the in-place confirmation step and rewrite the action's copy so it states the app password is destroyed too.

## 11. Typed confirmation on owner deletion

Added after the PR review flagged the asymmetry between a single-click owner
delete and the typed-word vault reset. See design decision 10a.

- [x] 11.1 Write failing bUnit tests on `OwnerTokens`: choosing delete opens a confirmation naming the owner and deletes nothing; the exact owner name deletes once; a near-miss, a case-different name, an empty entry, and a *different stored owner's* name each delete nothing and leave the confirmation open; cancelling deletes nothing and closes it; starting a second row's deletion leaves only that row confirming.
- [x] 11.2 Implement the per-row confirmation: `Ordinal` match against the row's own owner, one pending row at a time, typed text cleared when the pending row changes.
- [x] 11.3 Update the `settings-and-onboarding` spec with the confirmation requirement and its scenarios, and record design decision 10a.

## 12. Close-out

- [x] 12.1 Run the full solution build and every test project; fix any analyzer or `TreatWarningsAsErrors` fallout without suppressing rules.
- [x] 12.2 Run CSharpier `check` and the architecture tests -- confirm no Web type leaked into Core and the new Core types carry no infrastructure dependency.
- [x] 12.3 Collect coverage per the repo procedure and confirm the new Core and Persistence types are covered; delete `TestResults/`.
- [x] 12.4 Re-read `design.md` against what shipped; update any decision the implementation simplified away, then stop and wait for explicit approval before committing.
