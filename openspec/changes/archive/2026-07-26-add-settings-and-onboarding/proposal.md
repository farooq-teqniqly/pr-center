# Proposal: add-settings-and-onboarding

## Why

The app has a vault, a lock, a poll loop, and an inbox -- and no way to fill any
of them. `add-token-vault-and-lock` (#4) built `SetPasswordAsync`,
`StoreTokenAsync`, and `ResetVaultAsync`, but nothing calls the first two:
tokens can only be inserted by hand into SQLite, and a fresh install has no
reachable path from Uninitialized to a polling app. `add-review-queue-ui` (#6)
shipped a placeholder that tells the user to "open settings to set a password
and add tokens" (`UninitializedPlaceholder.razor`), pointing at a route that
does not exist.

This change builds that route: the first-run setup, the per-owner PAT entry, and
the poll-interval control. It closes the last gap before the app is usable
without a SQLite client, and it is the prerequisite for `add-containerization`
(#8), where hand-editing the database is not an option.

## What Changes

- **Settings page at `/settings`**, gated on app lock state. `Uninitialized`
  renders the setup card; `Locked` renders a short "unlock first" message
  pointing back to the inbox; `Unlocked` renders the tokens table, the poll
  interval control, and nothing else. The `Uninitialized` placeholder and the
  app bar link to it.
- **First-run setup.** A new `InitializeVault` Core use case composes
  `ITokenVault.SetPasswordAsync` + `IAppLock.UnlockAsync` + a refresh poke, so
  the user who just typed the password lands on the tokens table rather than
  being sent to re-enter it on the unlock card. The setup card enforces 8-32
  characters and a confirm field that must match; it *suggests* mixed case,
  digits, and symbols without enforcing them.
- **Per-owner PAT entry.** Add an owner (owner name + token), replace an
  existing owner's token, and delete an owner. The table shows owner, the
  instant the token was saved, and that owner's current fetch status projected
  from the published snapshot's `OwnerStatuses` -- so a bad token is diagnosed
  where it is fixed. Saving or deleting pokes `IRefreshTrigger`, and the status
  arrives with the next snapshot; settings itself never calls GitHub.
- **`ITokenVault.DeleteTokenAsync`**, new and gated on `Unlocked`. Without it an
  owner the user has left is wedged in the poll loop forever, failing every
  cycle with no way out short of a reset.
- **Token rows record when they were saved.** A `SavedAt` column on the owner
  token row, set on every store. There is no masked token fingerprint: one
  token per owner means the owner name already identifies the row, so the
  fingerprint would be plaintext derived from a secret bought for nothing.
- **Poll interval moves from configuration into the database.** A typed
  single-row app-settings table holds the interval; the `Polling` section of
  `appsettings.json`, `PollingOptions`, and `PollingOptionsValidator` are
  deleted. The default is 5 minutes when no row exists. The range stays 5
  minutes to 24 hours, now rejected on write (with a message) and clamped with a
  warning on read -- a bad stored value must not make the app unbootable, since
  the only UI to fix it is inside the app. Saving a new interval pokes
  `IRefreshTrigger`, so a shortened interval takes effect immediately instead of
  waiting out the sleep already in flight. The interval is readable while
  locked: it is not secret, the poll loop needs it, and
  `ITokenVault.ListOwnersAsync` already sets that precedent.
- **The reset action on the unlock card gets a typed confirmation and honest
  wording.** Today `UnlockCard` wires a link directly to `ResetVaultAsync`
  (`UnlockCard.razor:47`): one click, no confirmation, and the label reads
  "Reset stored tokens" when the action also destroys the app password. The
  action now expands to a confirmation step the user must type a fixed word into
  before the wipe runs, and says what it wipes. This change owns the fix because
  it makes first-run a flow the user can land back in, and because reset is the
  only path to a new password (there is no change-password in v1) -- so the
  action is both more reachable and more consequential than when it shipped.
- **The owner list stays derived from the token rows.** One owner = one token
  row; there is no separate owners table. Adding an owner *is* saving its token.
  This keeps `ListOwnersAsync` the single authoritative owner list the poll loop
  already reads, rather than introducing a second source of truth that can
  disagree with it.

## Capabilities

### New Capabilities

- `settings-and-onboarding`: the first-run setup flow, per-owner token
  management, the poll-interval control, and the lock-state gating of the
  settings surface.

### Modified Capabilities

- `token-vault`: token deletion, and a saved-at instant recorded per token row.
- `polling-and-refresh`: the poll interval is read from stored settings rather
  than application configuration, with write-time rejection and read-time
  clamping replacing startup validation.
- `review-queue-ui`: the `Uninitialized` placeholder links to the real settings
  route, and the unlock card's reset action requires a typed confirmation and
  states that it destroys the app password as well as the tokens.

## Non-goals

- **Changing the app password.** Not in v1. Re-keying would mean decrypting and
  re-encrypting every token under a new key; the only path to a new password is
  the reset that already exists.
- **A reset action in settings.** Reset stays on the unlock card, where
  `add-review-queue-ui` put it (hardened here, not moved), so the one
  unrecoverable action lives in exactly one place. Accepted consequence: reset is reachable only while `Locked`, so an
  unlocked user who wants to wipe must restart the process first. That is the
  forgot-password path, and forgetting the password means being locked.
- **Owner display ordering.** The inbox keeps the owner order it has today.
  User-specified ordering is [issue #28](https://github.com/farooq-teqniqly/pr-center/issues/28).
- **Synchronous token validation on save.** No GitHub call from the settings
  page; the next poll's `OwnerStatus` is the feedback channel. Validating inline
  would duplicate the fetch and its error taxonomy for one screen.
- **Any other setting.** Dark-mode toggle, notification preferences, and log
  levels are not in scope; the settings table gets exactly the interval.

## Impact

- `PrCenter.Core`: `InitializeVault` use case; `DeleteTokenAsync` on
  `ITokenVault`; a settings port for reading and writing the poll interval.
- `PrCenter.Persistence`: `SavedAt` on `OwnerToken`, the app-settings row and
  its adapter, and a migration for both. Reuses the real-SQLite-file integration
  harness.
- `PrCenter.Web`: the `/settings` page and its components, a nav link, the
  typed-confirm step on `UnlockCard`, and DI wiring. Deletes `Polling/PollingOptions.cs`,
  `Polling/PollingOptionsValidator.cs`, and the `Polling` configuration section;
  `QueuePollingService` reads the interval from the settings port each cycle.
- Tests: `PrCenter.Core.Tests` (`InitializeVault`, interval validation),
  `PrCenter.Persistence.Tests` (delete, `SavedAt`, settings round-trip against a
  real file), `PrCenter.Web.Tests` (bUnit: the three gated views, add/replace/
  delete, interval save, and the reset confirmation -- including that a
  cancelled or mistyped confirmation wipes nothing), minus the deleted
  `PollingOptionsValidator` tests.
