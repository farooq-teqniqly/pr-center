## Why

The skeleton left `TokenVault` throwing `NotImplementedException` and shipped no
app-lock code at all, yet the idea doc requires GitHub PATs encrypted at rest
with an app-password-derived key and an app that starts locked. Nothing can
poll GitHub or store a token until this crypto and the unlock flow exist. It is
roadmap change #4, built on the persistence foundation from #3.

## What Changes

- Real `TokenVault` replacing the stub: one fine-grained PAT per owner, stored
  AES-GCM-encrypted at rest, decrypted into memory only while unlocked. Deletes
  the stub throw-tests; adds real crypto + guard tests (mirrors how #3 closed
  the state-store part of issue #6).
- App password crypto: **Argon2id** derives the encryption key from the app
  password; a salt plus the Argon2 parameters (memory, iterations, parallelism)
  are stored, the password itself never is.
- Password verification with **no separate verifier hash**: at `SetPassword` the
  derived key encrypts a fixed known sentinel (AES-GCM); `Unlock` decrypts the
  sentinel and a failed authentication tag means a wrong password. Works even
  when zero tokens are stored yet.
- New **app-lock runtime FSM** with three states -- `Uninitialized` (no password
  set), `Locked` (password set, key not in memory), `Unlocked` (key in memory).
  A singleton key holder carries the decrypted key across Blazor circuits/tabs
  until the container stops; no idle auto-lock.
- New `IAppLock` port (session/key state) kept separate from `ITokenVault`
  (data at rest). `ITokenVault` gains `SetPassword`, `ResetVault`; `IAppLock`
  gains `Unlock`, `IsUnlocked`, current-state query.
- **BREAKING** to the current `ITokenVault` surface: `StoreTokenAsync` /
  `GetTokenAsync` keep their signatures but now require an unlocked vault and
  throw a `VaultLockedException` otherwise (defense in depth; polling is gated
  upstream in #5).
- `ResetVault` wipes the token rows and the security row (no-recovery path),
  returning the FSM to `Uninitialized`.
- Owns its schema: the token-record and app-security tables and their migration,
  on the #3 persistence foundation, designed here where the crypto format is set.
- Doc sweep: `docs/pr-center-state.md` and `docs/pr-center-architecture.md` app-lock
  sections are corrected from the two-state (`Locked`/`Unlocked`) picture to the
  three-state one, in the same pass (per the sweep-corrections rule).

## Capabilities

### New Capabilities
- `token-vault`: encrypted at-rest storage of one PAT per owner -- Argon2id KDF,
  AES-GCM cipher, sentinel-based password verification, the app-security and
  token tables, and `SetPassword` / `StoreToken` / `GetToken` / `ResetVault`.
- `app-lock`: the runtime lock FSM (`Uninitialized` -> `Locked` -> `Unlocked`),
  the singleton decrypted-key holder, the `Unlock` use case, and the gating that
  makes token access fail while locked.

### Modified Capabilities
<!-- None. #4 owns new token/security schema; it does not change the state-store
     marker spec's requirements. Doc updates to pr-center-state.md are prose, not
     an openspec spec delta. -->

## Non-goals

- No token/settings UI -- onboarding, PAT-entry, and reset screens are #7, which
  only calls the use cases delivered here. No crypto lands in #7.
- No polling or GitHub access wiring -- that is #5; this change only makes the
  vault gate-able.
- No idle auto-lock / session timeout (idea doc: none in v1).
- No password recovery or recovery key (idea doc: forgotten password means
  reset and re-enter the PATs).
- No key rotation or change-password flow in v1.
- No marker or token GC.

## Impact

- **Code:** `PrCenter.Core` (new `IAppLock` port, `Unlock`/`SetPassword`/
  `ResetVault` use cases, the app-lock FSM state type, `VaultLockedException`);
  `PrCenter.Persistence` (real `TokenVault`, Argon2id + AES-GCM crypto, singleton
  key holder, two new EF entities + migration, DI registration split: key holder
  singleton vs scoped vault); `PrCenter.Web` composition-root wiring.
- **Dependencies:** an Argon2id package (e.g. `Konscious.Security.Cryptography.Argon2`)
  added to `Directory.Packages.props`; container image size impact noted for #8.
- **Docs:** `docs/pr-center-state.md`, `docs/pr-center-architecture.md`.
- **Tests:** delete `TokenVaultTests` throw-tests; new crypto round-trip,
  wrong-password, zero-token-unlock, locked-access, and reset tests on the #3
  real-SQLite harness.
- **Issues:** closes GH issue #6 -- this change lands the last (`TokenVault`)
  part (stub throw-tests deleted, null guards + guard tests added); the
  `StateStore` part closed with #3 and the `GitHubFactsClient` part with #2.
