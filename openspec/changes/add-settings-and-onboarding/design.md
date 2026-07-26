# Design: add-settings-and-onboarding

## Context

The vault (`ITokenVault`), the lock (`IAppLock`), the poll loop
(`QueuePollingService`), and the inbox (`LockGate` + `InboxView`) all ship, but
`SetPasswordAsync` and `StoreTokenAsync` have no caller: the only way to get a
usable install today is to hand-insert rows into SQLite. `UninitializedPlaceholder.razor`
already tells the user to "open settings", and there is no `/settings` route.

Current state this design builds on:

- `LockGate.razor` reads `IAppLock.GetStateAsync()` and renders one of three
  branches (`InboxView`, `UnlockCard`, `UninitializedPlaceholder`). The inbox
  page is the only consumer.
- `UnlockCard.razor:47` wires a single click straight to `ITokenVault.ResetVaultAsync`,
  labelled "Reset stored tokens", with no confirmation.
- `UnlockApp` (`src/PrCenter.Core/Queue/UnlockApp.cs`) is the existing precedent
  for a thin Core use case that composes a port call with an `IRefreshTrigger`
  poke, so UI never orchestrates two ports itself.
- `PrCenterDbContext` holds `OwnerTokens` and the singleton `AppSecurity` row,
  and its own summary says "settings schema arrives with its own change".
- `QueuePollingService` reads the interval once, in its constructor, from
  `IOptions<PollingOptions>`, and arms a periodic `ITimer` in `StartAsync`.
- `PollingOptions.MinInterval`/`MaxInterval` (5 minutes / 24 hours) are enforced
  at startup by `PollingOptionsValidator` via `ValidateOnStart()`.

Constraint that shapes most decisions below: this is a self-hosted single-user
app whose only administrative UI is inside the app itself. Any failure mode that
prevents boot is unrecoverable without a SQLite client -- the exact dependency
this change exists to remove.

## Goals / Non-Goals

**Goals:**

- A reachable path from `Uninitialized` to a polling app with zero SQLite access.
- Per-owner token add/replace/delete, with each owner's fetch outcome visible on
  the same screen that fixes it.
- The poll interval stored in the database and editable in the app, replacing
  startup configuration.
- Reset made honest: says what it destroys, requires a typed confirmation.
- No new source of truth for the owner list -- `ListOwnersAsync` stays it.

**Non-Goals:**

- Changing the app password, a reset action inside settings, owner ordering,
  synchronous token validation on save, and any setting other than the interval
  (all per the proposal's Non-goals).
- Multi-user or role concepts. There is one user and one vault.
- A settings API surface for external callers. Settings is a Blazor page over
  Core use cases; there is no controller.

## Decisions

### 1. Thin Core use cases, not UI orchestration

`InitializeVault` (`SetPasswordAsync` + `IAppLock.UnlockAsync` + poke),
`SaveOwnerToken` (`StoreTokenAsync` + poke), `RemoveOwner` (`DeleteTokenAsync` +
poke), and `SavePollInterval` (settings write + poke) each live in
`PrCenter.Core`, mirroring `UnlockApp`.

Why: the poke is a rule about the domain ("stored facts changed, re-derive"),
not about a page. Putting it in the component means the next caller -- a future
CLI, a container health path -- silently loses it. `UnlockApp` already
established the shape and the test seam.

Alternative considered: one `ManageSettings` facade holding all four operations.
Rejected -- it would carry four unrelated dependencies for any caller that needs
one, and each operation has a distinct precondition (uninitialized vs unlocked).

`InitializeVault` deliberately unlocks after setting the password. `SetPasswordAsync`
does not unlock by contract, so without this composition the user who just typed
a password would be bounced to the unlock card to type it again.

### 2. The poll interval is a value object, validated at construction

`PollInterval` in `PrCenter.Core` is a `readonly record struct` wrapping a
`TimeSpan`, with `Min` (5 minutes) and `Max` (24 hours) constants, a constructor
that throws for out-of-range values, and a static `Clamp(TimeSpan)` that returns
the nearest in-range value. It satisfies the baseline's `readonly record struct`
exception: one wrapped primitive, a range-only invariant, structural equality
correct.

The settings port takes and returns `PollInterval`, never a raw `TimeSpan`, so
an out-of-range interval is unrepresentable past the boundary. The settings page
range-checks the user's input against `PollInterval.Min`/`Max` before
constructing one and renders its own message on failure -- validation messages
are UI text, not domain concerns, so the domain does not carry a string.

Alternative considered: `TryCreate` with an `out string? error`. Rejected -- it
pushes UI copy into the domain and produces a two-out signature.

### 3. Write rejects, read clamps -- deliberately asymmetric

Writes go through `PollInterval`, so an out-of-range value never reaches storage
from the app. Reads clamp with a `Warning` log instead of throwing.

Why the asymmetry: `PollingOptionsValidator` + `ValidateOnStart()` is being
deleted precisely because it fails the process at startup, and the only UI that
could fix a bad value now lives inside that process. A row hand-edited to
`0` seconds must degrade to a 5-minute poll and a warning, never to an
unbootable app. The clamp warning is emitted on each read (once per poll cycle);
that repetition is acceptable and is itself the signal that a stored value needs
fixing.

### 4. A singleton settings row, storing seconds as an integer

`AppSetting` follows the `AppSecurity` pattern: `Id` with `ValueGeneratedNever()`,
always `1`, "a row exists" as the discriminator. Its single column is
`PollIntervalSeconds` (`long`). Absent row means the 5-minute default; the
adapter does not seed a row at migration time.

Why seconds-as-integer rather than EF's default `TimeSpan` mapping: SQLite has
no time type, and EF stores `TimeSpan` as a formatted TEXT string that a human
inspecting or repairing the file cannot reliably edit. An integer count of
seconds is unambiguous in a SQLite client, which matters for the one file the
user may still have to touch.

Alternative considered: a generic key/value settings table. Rejected -- the
proposal caps the table at exactly one setting, and a string-keyed store gives
up typing and migration checking for flexibility that is explicitly out of scope.

### 5. The interval is readable while locked; writing it is not vault-gated

The settings adapter reads and writes without touching the vault key, so
`GetPollIntervalAsync` works in every lock state. Precedent:
`ITokenVault.ListOwnersAsync` already reads plaintext regardless of lock state,
and the poll loop needs the interval before it knows whether it is unlocked.

Writes are not gated on `Unlocked` at the port either. The interval is not a
secret and needs no key; the only writer is a page that lock-gating already puts
behind `Unlocked`. Adding a `VaultLockedException` path to a port that needs no
key would be theatre -- a guard that proves nothing about confidentiality.

### 6. `DeleteTokenAsync` *is* vault-gated on `Unlocked`

Unlike the settings write, deletion joins `StoreTokenAsync`/`GetTokenAsync` in
throwing `VaultLockedException` when locked. It is a destructive act on secret
material, and the gate keeps the vault's rule uniform: mutating the token set
requires having proved the password. `ResetVaultAsync` stays the sole exception,
because it is the forgot-password path and by definition cannot require the
password.

### 7. `SavedAt` is nullable

A nullable `SavedAt` (`DateTimeOffset?`) on `OwnerToken`, set on every
`StoreTokenAsync` from an injected `TimeProvider` (`TokenVault` gains the
dependency; the singleton `TimeProvider.System` is already registered in
`AddQueueServices`). Rows predating the migration read `null` and the table
renders "Unknown".

Why not non-nullable with a backfill: any backfill value is a fabricated claim
about when a token was saved. `null` says "not recorded", which is true.
Alternative considered -- backfilling the migration instant -- was rejected
because the tokens table exists to help the user reason about a stale or revoked
token, and a wrong timestamp there actively misleads.

### 8. `LockGate` generalizes; it is not copied

`LockGate` gains optional `Locked` and `Uninitialized` `RenderFragment`
parameters that default to today's `UnlockCard` and `UninitializedPlaceholder`,
so `Inbox.razor` is unchanged. `/settings` supplies its own two: a setup card
for `Uninitialized`, and a short "unlock first" message linking back to `/` for
`Locked`.

Why: state-reading and re-evaluation-after-transition is the part that is easy
to get subtly wrong (the `OnUnlocked`/`OnReset` callbacks re-read state), and it
should exist once. A second gate component would duplicate that logic with no
shared test.

### 9. The poll timer re-arms per cycle from stored settings

`QueuePollingService` stops taking `IOptions<PollingOptions>`. Its `ITimer`
becomes one-shot: armed in `StartAsync` and re-armed after each wake with the
interval read inside that wake's DI scope. The trigger-and-single-loop model is
untouched -- the timer remains just another poker of the one trigger, so
coalescing and non-overlap still hold.

Consequence, intended: because the timer re-arms after every wake, an on-demand
refresh (unlock, manual, a settings save) restarts the interval clock. The
interval means "time since the last poll", not "position in a fixed schedule",
which is the right reading for a poll whose only purpose is freshness.

`SavePollInterval` pokes the trigger so a shortened interval takes effect at
once rather than after the sleep already in flight expires.

### 10. Reset gets a typed confirmation, in place

The reset control on `UnlockCard` expands in place into a confirmation step: the
user types a fixed word before the destructive call runs, and the copy states
that the app password is destroyed along with the tokens. Cancelling, or
submitting a mismatched word, calls nothing.

Kept on the unlock card rather than moved into settings, per the proposal: the
one unrecoverable action lives in exactly one place, and that place is the one
the locked-out user actually reaches.

### 11. The tokens table shows status and `SavedAt`, never `LastFreshAt`

`OwnerStatus.LastFreshAt` is read by the inbox and stays there. Settings shows
the owner, the status chip, and when the token was saved -- the three facts that
bear on "is this token good". Staleness of the *rows* is an inbox concern;
repeating it here would be a second place to maintain the same reading with no
new decision attached to it.

### 12. Input validation is shape-only -- length, never GitHub

Neither the owner field nor the token field is checked against GitHub. The page
rejects only what is obviously unusable before a round trip:

- **Owner:** non-empty after trimming, at most 255 characters -- the width of
  the `OwnerToken.Owner` column, so the rejection message beats a database
  truncation error.
- **Token:** non-empty, and a generous upper bound (512 characters) that no real
  fine-grained PAT approaches. This catches a pasted file or a truncated
  clipboard, not a malformed token.

Deliberately absent: GitHub's login charset and 39-character login limit, and
any `ghp_`/`github_pat_` prefix check. Those encode GitHub's current formats
into this app, and GitHub has changed both before. The next poll's `OwnerStatus`
is the authority on whether an owner and token actually work -- consistent with
the proposal's rule that settings never calls GitHub.

Trade-off accepted: a typo'd owner name is diagnosed one poll later rather than
at the keystroke. The save pokes the trigger, so that is one fetch, and the
status lands in the same table the user is already looking at.

## Risks / Trade-offs

- **A stored interval that is valid but useless (e.g. 24 hours) makes the app
  look broken.** → The interval control shows the current value and its allowed
  range, and manual refresh remains available, so a long interval is visible and
  survivable rather than mysterious.
- **The clamp warning repeats every poll cycle for a bad stored row.** →
  Accepted. This is a single-user self-hosted app where the log is read by the
  person who can fix it, and suppressing repeats would hide a persistent fault.
- **Deleting the `Polling` configuration section is a breaking change for any
  existing `appsettings.json` override.** → The section is removed rather than
  silently ignored; a leftover key binds to nothing and is inert. The app has
  not shipped in a container yet, so the blast radius is the developer
  workstation.
- **Token status on the settings page is one poll stale.** → By design (the
  proposal rules out synchronous validation). The save pokes the trigger, so the
  wait is the length of one fetch, not one interval. The table shows what the
  last poll reported, so a just-saved owner reads as "not yet polled" until the
  snapshot arrives.
- **`InitializeVault` performs two writes plus an unlock and is not atomic.** →
  If `UnlockAsync` fails after `SetPasswordAsync` succeeds, the vault is
  initialized but the app is `Locked` -- a recoverable state whose UI (the
  unlock card) already exists, and the password the user just typed is the one
  that works. No compensation logic is warranted.
- **A migration adding two schema objects (the `SavedAt` column and the settings
  table) touches a file holding the user's only copy of their encrypted
  tokens.** → Both are additive; neither rewrites `OwnerToken` secret columns.
  The persistence integration tests exercise the migration against a real SQLite
  file, per the existing harness.
- **Generalizing `LockGate` risks regressing the inbox.** → The new parameters
  default to the current fragments, so `Inbox.razor` needs no edit; the existing
  gate tests stand as the regression check.
