## 1. Dependencies and schema

- [x] 1.1 Add `Konscious.Security.Cryptography.Argon2` to `Directory.Packages.props` and reference it in `PrCenter.Persistence`
- [x] 1.2 Add the `OwnerToken` EF entity (`Owner` PK, `Nonce`, `Ciphertext`, `Tag`) and `AppSecurity` entity (single-row PK, `Salt`, `MemoryKib`, `Iterations`, `Parallelism`, `KdfVersion`, sentinel `Nonce`/`Ciphertext`/`Tag`) configured via Fluent API in `OnModelCreating`
- [x] 1.3 Add one EF migration creating both tables; confirm startup migration (from #3) applies it while Locked

## 2. Crypto primitives (Persistence, TDD)

- [x] 2.1 Failing tests: Argon2id key derivation is deterministic for the same password+salt+params and differs for a different password
- [x] 2.2 Implement the Argon2id key-derivation helper (32-byte output, params from a security row)
- [x] 2.3 Failing tests: AES-GCM encrypt/decrypt round-trips; wrong key fails the auth tag; each encrypt uses a fresh nonce
- [x] 2.4 Implement the AES-GCM encrypt/decrypt helper producing/consuming `{ nonce, ciphertext, tag }`

## 3. App-lock port and key holder (Core + Persistence, TDD)

- [x] 3.1 Add `IAppLock` port to `PrCenter.Core` (`GetStateAsync`) and the three-state `AppLockState` type (`Uninitialized`/`Locked`/`Unlocked`); add `VaultLockedException`. (`UnlockAsync` deferred to task 4, where it is implemented and tested together -- adding it here would ship a stub, which issue #6 forbids.)
- [x] 3.2 Failing tests: state derives as Uninitialized (no security row), Locked (row, no key), Unlocked (key held); shared singleton holder returns the same key across resolutions
- [x] 3.3 Implement the singleton key holder / `IAppLock` implementation (holds `byte[]` key + derived state; zeroes key on clear)

## 4. Vault: set password and verification (Persistence, TDD)

- [x] 4.1 Failing tests: `SetPasswordAsync` first-run writes salt/params/encrypted sentinel; rejects when a security row already exists
- [x] 4.2 Implement `SetPasswordAsync` on `TokenVault` (generate salt, derive key, encrypt sentinel, persist security row)
- [x] 4.3 Add `UnlockAsync` to the `IAppLock` port; failing tests: it accepts the correct password, rejects a wrong one (state stays Locked, no key), rejects when Uninitialized, and verifies correctly with zero tokens stored
- [x] 4.4 Implement unlock/verify (re-derive key, decrypt sentinel, hold key in the singleton on success)

## 5. Vault: token store, retrieve, gating, reset (Persistence, TDD)

- [x] 5.1 Delete the `TokenVaultTests` `NotImplementedException` throw-tests
- [x] 5.2 Failing tests: `StoreTokenAsync` encrypts and persists per owner; storing again for the same owner replaces it; plaintext never in a persisted column
- [x] 5.3 Failing tests: `GetTokenAsync` decrypts a stored token; returns null when none stored
- [x] 5.4 Failing tests: store/get throw `VaultLockedException` while Locked or Uninitialized
- [x] 5.5 Implement `StoreTokenAsync`/`GetTokenAsync` (AES-GCM under the held key; guard on unlocked state; owner upsert)
- [x] 5.6 Failing tests: `ResetVaultAsync` deletes all token rows and the security row without requiring unlock; discards the in-memory key; state returns to Uninitialized
- [x] 5.7 Implement `ResetVaultAsync`
- [x] 5.8 Add null/whitespace guard tests for every public/internal entry point (owner, token, password) and implement the guards

## 6. Wiring and docs

- [x] 6.1 Split DI in `AddPersistenceAdapter`: `AddSingleton` for the `VaultKeyHolder`, `AddScoped` for `IAppLock` and `TokenVault` (the lock and vault both need the scoped `DbContext`, so only the key holder is a singleton); add a DI composition-root test that `IAppLock` resolves to the Persistence adapter
- [x] 6.2 Add source-generated `[LoggerMessage]` logging for the unlock-failed (`AppLock.Logging.cs`) and reset (`TokenVault.Logging.cs`) paths
- [x] 6.3 Sweep docs: update `docs/pr-center-state.md` section 3 and `docs/pr-center-architecture.md` app-lock notes from two states to the three-state FSM (diagram + prose); add `IAppLock` to the ports list in the architecture prose
- [x] 6.4 Sweep the canonical Lucid architecture diagram (`bc093d17-51ce-4975-a398-caebcb69d817`, "PR-Center Architecture (revised)"): added `IAppLock` to the ports box (now `IGitHubFacts | IStateStore | ITokenVault | IAppLock`) and to the Persistence "implements" edge label, per the "update both in the same pass" rule in pr-center-architecture.md
- [x] 6.5 Run the full test + coverage pass on the changed projects per CLAUDE.md; confirm coverage and quality gate before archiving
- [ ] 6.6 Close GH issue #6 (deferred to PR merge): this change lands the final (`TokenVault`) part -- stub throw-tests deleted (5.1) and null guards + guard tests added (5.8). The `StateStore` part closed with #3 and the `GitHubFactsClient` part with #2, so with #4 merged all three files are done. Put "Closes #6" in the PR body so merge auto-closes it (not closed pre-merge, so a revert would not leave a wrongly-closed issue)
