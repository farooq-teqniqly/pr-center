# Design: add-review-queue-ui

## Context

The published `QueueSnapshot` now carries every datum the inbox rows need, and
the #4 lock surface exposes the unlock flow. Verified against the code, not
inferred:

- `GetQueue.Execute()` returns `QueueSnapshot?` -- null is the never-polled
  state, distinct from a polled-empty snapshot. Items arrive flat; grouping and
  sort are explicitly a UI concern per the `QueueItem` and `QueueItemDeriver`
  contracts.
- `QueueItem` exposes `Identity` (incl. `AuthorLogin`, `Url`), `LastUpdate`
  (`By`/`At`), `State`, `HasUpdate`, `Roster` (`ReviewerRosterEntry`: login,
  `ReviewerState`, `IsBot`, `IsMe`), `MyEngagement` (`LastReviewedAt?` -- the
  sole engagement instant since `replace-marker-with-review-baseline` dropped
  `LastLookedAt`), and `CoveredBy` (+ derived `IsAlreadyCovered`).
- `HasUpdate` is derived against the user's latest review instant, not a stored
  marker: a never-reviewed PR has a null baseline and `HasUpdate` is false (it
  is *new*, not *updated*). No `MarkSeen` use case exists to call anymore.
- `OwnerStatus` exposes `Owner`, `Status` (`Ok | MisconfiguredToken | Error`),
  `Detail?`, and `LastFreshAt?` for the stale label.
- `IAppLock.GetStateAsync()` returns `AppLockState`
  (`Uninitialized | Locked | Unlocked`). `UnlockApp.UnlockAsync(password)`
  returns bool and pokes `IRefreshTrigger` on success.
  `ITokenVault.ResetVaultAsync()` backs the reset link.
- `QueueSnapshotHolder.Publish` swaps an immutable snapshot by atomic reference;
  it has no change notification today -- the one Core gap this change closes.

## Goals / Non-Goals

**Goals:**

- Render the four mockup screens (unlock, inbox, empty, and the never-polled
  variant) from Core use cases only, with no facts/marker/derivation access in
  the UI.
- Re-render exactly when a new snapshot is published, with no UI polling timer.
- Port the mockup's *semantic* styling -- the mapping from derived state to
  color and shape -- as the presentation contract.

**Non-Goals:**

- App-password setup, PAT entry, settings, owner management -- #7.
- Byline activity verbs and reply-target facts -- not derivable.
- Pixel/typography fidelity and an explicit theme toggle.

## Decisions

### D1. Lock gate wraps the whole app; three states, three renders

A gate component reads `IAppLock.GetStateAsync` before rendering the inbox:

- `Unlocked` -> inbox.
- `Locked` -> unlock card (mockup 01). Submit calls `UnlockApp.UnlockAsync`; a
  false result shows the wrong-password message and stays; a true result
  re-evaluates the gate (now `Unlocked`) and the already-poked refresh trigger
  fills the first snapshot.
- `Uninitialized` -> a placeholder directing the user to settings to set a
  password and add tokens. Setup is #7; this change does not build it, only
  refuses to pretend the inbox is usable. The reset link on the unlock card
  calls `ITokenVault.ResetVaultAsync` and returns the app to `Uninitialized`.

The gate re-checks state on unlock and on reset; it does not poll.

### D2. Freshness is a holder event, not a timer

`QueueSnapshotHolder` gains `event EventHandler? Changed`, raised inside
`Publish` after the reference swap. The inbox subscribes in `OnInitialized`,
and the handler marshals to the render thread via `InvokeAsync(StateHasChanged)`
before reading `GetQueue.Execute()` again; it unsubscribes in `Dispose`
(`IDisposable` component). Rationale: the SignalR circuit is the render
transport, not the freshness trigger (idea doc). A timer would re-render on a
fixed clock regardless of whether data changed, adding lag and wasted renders;
the event re-renders precisely on publish. The holder stays the single
publication point, so the event has exactly one raise site.

Thread-safety: `Publish` already uses `Volatile.Write`; raising after the write
means a subscriber that reads `Current` in the handler always sees the new
snapshot. Subscribers run on the poll thread, so the handler does no work beyond
`InvokeAsync`.

### D3. Click-through is a plain anchor, no side effect

The title link is a real anchor to `Identity.Url` with `target="_blank"` so
GitHub opens in a new tab and the Blazor circuit survives. That is the whole
interaction: there is no mark-as-seen dispatch, no click-time live fetch, no
local mutation. `replace-marker-with-review-baseline` deleted the `MarkSeen`
use case and the stored marker precisely because opening-and-not-reading wrote
"seen" for changes the user never saw. The update badge now clears only on the
next published snapshot after the user actually reviews the PR on GitHub --
the safe over-notify direction. Nothing on click touches server state.

### D4. Byline collapses to who-and-when

The mockup's "pushed 2 commits" / "replied to my comment" verbs need an
activity summary and reply-target facts that Core does not carry (recorded as
non-goals in #5b). The byline renders `{LastUpdate.By} . {relative(At)}` only
-- e.g. "dkellner . 22 min ago". "opened" is not asserted either, since the
facts do not distinguish opening from a later push; who-last-touched-and-when
is the honest projection. Relative-time formatting is a pure UI helper.

### D5. Bootstrap + Inter scaffold; semantic tokens are the presentation contract

`App.razor` already loads Bootstrap 5.3.3 (CSS + JS bundle, CDN, SRI-pinned) and
the Inter web font (Google Fonts CDN). This change uses them rather than
hand-rolling structure and typography: Bootstrap owns layout, cards, badges,
buttons, and utility spacing; Inter is the type face (superseding the earlier
`system-ui` fallback plan). Custom CSS is reduced to what Bootstrap cannot
express -- the mockup's *semantic state palette*.

Those color and shape tokens *encode derived state* -- they are how the
projection becomes legible, not decoration. Each is a custom token layered over
Bootstrap; these map one-to-one and are in scope:

| Visual | Backing field |
|--------|---------------|
| amber stripe + "Updated" badge | `QueueItem.HasUpdate` |
| roster chip color (green/red/blue/gray) | `ReviewerRosterEntry.State` |
| dashed "me" ring on a chip | `ReviewerRosterEntry.IsMe` |
| bot chip treatment | `ReviewerRosterEntry.IsBot` |
| "covered . a, b" decoration | `QueueItem.CoveredBy` |
| owner chip ok/stale + "stale {t}" | `OwnerStatus.Status` / `LastFreshAt` |
| error banner | any non-`Ok` `OwnerStatus` |

Explicitly out of scope: pixel-exact hex matching and an in-app light/dark
*toggle button* (that is #7).

Dark mode is **auto**, following the OS/browser setting, with no toggle. Both
light and dark token sets ship. The wrinkle: Bootstrap 5.3 color modes are
attribute-driven (`data-bs-theme`), not `prefers-color-scheme`-driven, so a
media query alone would darken the custom tokens while leaving Bootstrap's own
surfaces light. To keep both in sync, a tiny inline script in `App.razor`'s
`<head>` sets `document.documentElement`'s `data-bs-theme` to `dark`/`light`
from `matchMedia('(prefers-color-scheme: dark)')` at load, and both Bootstrap
and the custom tokens key off that attribute. The mockup's `:root` light tokens
move into `wwwroot/app.css` with a `[data-bs-theme="dark"]` override block for
the dark set (replacing the mockup's `@media` query and its own `data-theme`
toggle hook); per-component rules live in scoped `.razor.css` files. When #7
adds an explicit toggle it flips the same `data-bs-theme` attribute, so no
retheming work is deferred -- only the button.

### D6. Grouping and intra-group sort live in the UI

`Items` is flat. The inbox groups by `Identity.Owner` then `Identity.Repository`
and, within a group, sorts `HasUpdate` desc (unseen first) then `LastUpdate.At`
desc (most recent first) -- confirmed here per the idea doc, which parked the
intra-group tiebreaker for implementation. Group order follows the owner order
as configured (the `OwnerStatuses` sequence), so the list order is stable across
polls. Sorting is a pure projection over the snapshot; no Core change.

### D7. Component decomposition

```
LockGate                       reads IAppLock; picks Unlock | Uninitialized | Inbox
  UnlockCard                   UnlockApp + ResetVault
  Inbox (@page "/")            subscribes to holder Changed; reads GetQueue
    AppBar                     polled/next stamps, Refresh -> IRefreshTrigger
    OwnerChips                 OwnerStatuses
    ErrorBanner                non-Ok owners
    EmptyState / NeverPolled   provable-empty vs null snapshot
    QueueGroup (per org/repo)  GroupHead + rows
      QueueRow                 stripe, title link (plain anchor), byline, RosterChips, times
        RosterChips            ReviewerRosterEntry list
```

Each component takes its slice of the snapshot as a parameter; only `Inbox`
and `LockGate` touch use cases, keeping the leaf components pure render.

## Risks / Trade-offs

- [Holder event raised on the poll thread] -> handler must stay trivial
  (`InvokeAsync` only); heavier work on the poll thread would slow polling.
  Bounded by contract: the only subscriber is the inbox.
- [Badge persists until I review] -> a PR read on GitHub but not yet re-reviewed
  keeps its badge across polls. This is the intended over-notify direction that
  `replace-marker-with-review-baseline` chose over the click-through model, not
  a regression; the PR genuinely still awaits my review.
- [Byline honesty vs mockup] -> the rendered byline is less specific than the
  mockup's illustrative verbs; the mockup oversold data that never existed.
- [Semantic color as sole signal] -> color-only encoding is an accessibility
  gap; the "Updated" badge text and chip labels carry the same meaning in text,
  so state is never color-only. No extra work needed.
- [`Uninitialized` placeholder depends on #7] -> until #7 ships, that state is a
  dead-end message; acceptable, the vault is initialized out-of-band today.

## Migration Plan

One Core change (`QueueSnapshotHolder.Changed`), additive -- existing publishers
and the `RefreshQueue` caller are unaffected. All other work is new Web
components and CSS. No persistence or wire migration. Single PR; bUnit tests
alongside. Rollback = revert.

## Open Questions

- None blocking. The `Uninitialized` placeholder's exact copy and the settings
  route it links to are finalized when #7 defines the settings page.
