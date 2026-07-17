# Proposal: add-review-queue-ui

## Why

Everything the review inbox needs now exists in Core: `add-queue-enrichment`
(#5b) grew `QueueItem` with author, roster, engagement stamps, and covering
names, and `add-token-vault-and-lock` (#4) exposed the lock/unlock surface.
What is missing is the inbox itself -- the user still sees a placeholder Home
page. This change renders the published `QueueSnapshot` as the four-screen
experience in the UX mockups (docs/pr-center-ux-mockups.html), presentation-only:
no derivation, no facts access, no I/O beyond the Core use cases already built.

## What Changes

- **Unlock screen** (mockup 01). Renders when `IAppLock.GetStateAsync` reports
  `Locked`; the password field calls `UnlockApp.UnlockAsync` (which already
  pokes the refresh trigger on success), and the "reset stored tokens" link
  calls `ITokenVault.ResetVaultAsync`. `Uninitialized` routes to a placeholder
  pointing at settings (setup flow is #7), never builds setup here.
- **Review inbox** (mockup 02). Reads `GetQueue.Execute()`, groups items by
  owner then repo, sorts unseen-first then most-recently-updated within a
  group, and renders each row: amber stripe/badge from `HasUpdate`, collapsed
  byline (`{LastUpdate.By} . {LastUpdate.At}`), reviewer roster chips colored by
  `ReviewerState` with a dashed ring for `IsMe`, covered decoration naming
  `CoveredBy`, and the three time stamps from `MyEngagement` + `LastUpdate`.
  Title click opens the PR on GitHub and dispatches `MarkSeen` (not awaited).
- **Owner chips + error banner.** `OwnerStatuses` render as per-owner chips;
  any non-`Ok` owner raises a labeled banner and a "stale {LastFreshAt}" chip,
  with the owner's carried rows still shown.
- **Empty and never-polled states** (mockup 03). "All caught up" when polled and
  empty; owner chips stay visible so a provable-empty is distinct from a
  failed-fetch-empty. Null snapshot (never polled since start) shows a distinct
  "polling has not run yet" state.
- **Freshness without a timer.** A `Changed` event on `QueueSnapshotHolder`
  fires on publish; the inbox subscribes and re-renders over the existing Blazor
  circuit. No polling timer in the UI.
- **Semantic styling from the mockup** ported into `wwwroot/app.css` (token
  block) and scoped `.razor.css` files -- the state-to-color mapping is the
  presentation contract, not decoration (see design D5).

## Capabilities

### New Capabilities

- `review-queue-ui`: the Blazor presentation of the review inbox -- unlock gate,
  grouped/sorted list, row rendering, owner status surface, empty states, and
  the click-through-marks-seen interaction.

### Modified Capabilities

- `polling-and-refresh`: `QueueSnapshotHolder` raises a `Changed` event on
  publish so observers re-render on new snapshots without polling.

## Non-goals

- Settings page, PAT entry, owner management, poll-interval UI, and app-password
  *setup* -- all `add-settings-and-onboarding` (#7); this change only routes to
  it from the `Uninitialized` gate.
- Byline activity summaries ("pushed 2 commits") and reply-target detection
  ("replied to my comment") -- the facts do not exist; byline collapses to
  who-and-when.
- Exact typographic fidelity to the mockup (Segoe stack) and an explicit
  light/dark toggle -- system font and `prefers-color-scheme` only.
- Any PR mutation, sort tiebreaker on the covered flag, or marker GC.

## Impact

- `PrCenter.Web`: new `Components/Pages` (inbox, unlock), `Components/Queue`
  (group, row, roster, owner chips, banner, empty states), scoped `.razor.css`,
  the token block in `wwwroot/app.css`, and DI wiring for `GetQueue`,
  `MarkSeen`, `UnlockApp`, `IAppLock`, `ITokenVault`, `IRefreshTrigger`.
- `PrCenter.Core`: `QueueSnapshotHolder` gains the `Changed` event (only
  non-UI production change).
- Tests: `PrCenter.Web.Tests` (bUnit component tests, auth simulated via
  `TestAuthorizationContext`), `PrCenter.Core.Tests` (holder event).
- No persistence schema change.
