## 1. Dependencies and schema

- [x] 1.1 Add `Konscious.Security.Cryptography.Argon2` to `Directory.Packages.props` and reference it in `PrCenter.Persistence`
- [x] 1.2 Add the `OwnerToken` EF entity (`Owner` PK, `Nonce`, `Ciphertext`, `Tag`) and `AppSecurity` entity (single-row PK, `Salt`, `MemoryKib`, `Iterations`, `Parallelism`, `KdfVersion`, sentinel `Nonce`/`Ciphertext`/`Tag`) configured via Fluent API in `OnModelCreating`
- [x] 1.3 Add one EF migration creating both tables; confirm startup migration (from #3) applies it while Locked

## 2. Crypto primitives (Persistence, TDD)

- [ ] 2.1 Failing tests: Argon2id key derivation is deterministic for the same password+salt+params and differs for a different password
- [ ] 2.2 Implement the Argon2id key-derivation helper (32-byte output, params from a security row)
- [ ] 2.3 Failing tests: AES-GCM encrypt/decrypt round-trips; wrong key fails the auth tag; each encrypt uses a fresh nonce
- [ ] 2.4 Implement the AES-GCM encrypt/decrypt helper producing/consuming `{ nonce, ciphertext, tag }`

## 3. App-lock port and key holder (Core + Persistence, TDD)

- [ ] 3.1 Add `IAppLock` port to `PrCenter.Core` (`UnlockAsync`, `CurrentState`) and the three-state `AppLockState` type (`Uninitialized`/`Locked`/`Unlocked`); add `VaultLockedException`
- [ ] 3.2 Failing tests: state derives as Uninitialized (no security row), Locked (row, no key), Unlocked (key held); shared singleton holder returns the same key across resolutions
- [ ] 3.3 Implement the singleton key holder / `IAppLock` implementation (holds `byte[]` key + derived state; zeroes key on clear)

## 4. Vault: set password and verification (Persistence, TDD)

- [ ] 4.1 Failing tests: `SetPasswordAsync` first-run writes salt/params/encrypted sentinel; rejects when a security row already exists
- [ ] 4.2 Implement `SetPasswordAsync` on `TokenVault` (generate salt, derive key, encrypt sentinel, persist security row)
- [ ] 4.3 Failing tests: `UnlockAsync` accepts the correct password, rejects a wrong one (state stays Locked, no key), rejects when Uninitialized, and verifies correctly with zero tokens stored
- [ ] 4.4 Implement unlock/verify (re-derive key, decrypt sentinel, hold key in the singleton on success)

## 5. Vault: token store, retrieve, gating, reset (Persistence, TDD)

- [ ] 5.1 Delete the `TokenVaultTests` `NotImplementedException` throw-tests
- [ ] 5.2 Failing tests: `StoreTokenAsync` encrypts and persists per owner; storing again for the same owner replaces it; plaintext never in a persisted column
- [ ] 5.3 Failing tests: `GetTokenAsync` decrypts a stored token; returns null when none stored
- [ ] 5.4 Failing tests: store/get throw `VaultLockedException` while Locked or Uninitialized
- [ ] 5.5 Implement `StoreTokenAsync`/`GetTokenAsync` (AES-GCM under the held key; guard on unlocked state; owner upsert)
- [ ] 5.6 Failing tests: `ResetVaultAsync` deletes all token rows and the security row without requiring unlock; discards the in-memory key; state returns to Uninitialized
- [ ] 5.7 Implement `ResetVaultAsync`
- [ ] 5.8 Add null/whitespace guard tests for every public/internal entry point (owner, token, password) and implement the guards

## 6. Wiring and docs

- [ ] 6.1 Split DI in `AddPersistenceAdapter`: `AddSingleton` for the key holder / `IAppLock`, `AddScoped` for `TokenVault`; update `PrCenter.Web` composition root and its DI composition-root test
- [ ] 6.2 Add source-generated `[LoggerMessage]` logging for the unlock-failed and reset paths (`Foo.Logging.cs` split)
- [ ] 6.3 Sweep docs: update `docs/pr-center-state.md` section 3 and `docs/pr-center-architecture.md` app-lock notes from two states to the three-state FSM (diagram + prose); add `IAppLock` to the ports list in the architecture prose
- [ ] 6.4 Sweep the canonical Lucid architecture diagram (`bc093d17-51ce-4975-a398-caebcb69d817`, "PR-Center Architecture (revised)"): add `IAppLock` to the ports box alongside `IGitHubFacts | IStateStore | ITokenVault`, per the "update both in the same pass" rule in pr-center-architecture.md
- [ ] 6.5 Run the full test + coverage pass on the changed projects per CLAUDE.md; confirm coverage and quality gate before archiving
- [ ] 6.6 Close GH issue #6: this change lands the final (`TokenVault`) part -- stub throw-tests deleted (5.1) and null guards + guard tests added (5.8). The `StateStore` part closed with #3 and the `GitHubFactsClient` part with #2, so with #4 merged all three files are done. Comment the completion on the issue and close it
