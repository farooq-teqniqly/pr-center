# Design: add-token-vault-and-lock

## Context

The skeleton left `TokenVault` throwing and no app-lock code, but the idea doc
([pr-center-idea.md](../../../docs/pr-center-idea.md), "Tokens encrypted at rest")
fixes the shape: an app password derives an encryption key via a KDF, tokens are
AES-encrypted at rest, the password is never stored (only a salt and a verifier),
and the same password is the app's access gate. [pr-center-state.md](../../../docs/pr-center-state.md)
section 3 draws the app-lock FSM, and [pr-center-architecture.md](../../../docs/pr-center-architecture.md)
places the FSM in Core ("the only true runtime FSM") and the crypto in
`PrCenter.Persistence` behind `ITokenVault`. This change builds on #3's migration,
SQLite-configuration, and real-SQLite test harness; it owns the token and
app-security tables designed here where the crypto format is decided.

## Goals / Non-Goals

**Goals:**

- Real `TokenVault`: Argon2id-derived key, AES-GCM at rest, one PAT per owner.
- Sentinel-based password verification that works with zero tokens stored.
- The three-state app-lock FSM and a process-wide decrypted-key holder.
- Token access gated: store/get throw while not Unlocked.
- No-recovery `ResetVault`. Own schema + migration on the #3 foundation.

**Non-Goals:** any UI (#7), polling/GitHub wiring (#5), idle auto-lock,
password change/rotation, password recovery, token/marker GC.

## Decisions

### D1: Argon2id KDF, parameters stored per security row

Argon2id (memory-hard) over PBKDF2, chosen for resistance to offline GPU
cracking of the app password -- the one secret guarding volume-at-rest theft.
Package: `Konscious.Security.Cryptography.Argon2` (managed, no native lib), added
to `Directory.Packages.props`. The `Salt`, `MemoryKib`, `Iterations`,
`Parallelism`, and a `KdfVersion` are stored on the security row so parameters
can be raised later without breaking existing rows (the row carries the
parameters it was written with). Argon2 output length = 32 bytes (AES-256 key).

- *Alternative: PBKDF2-SHA256 (in-box, zero deps).* Rejected -- weaker per unit
  time against GPU; the container-size cost of a managed Argon2 package is
  acceptable and noted for #8.

### D2: AES-GCM with a per-ciphertext random nonce

Authenticated encryption (AES-256-GCM via `System.Security.Cryptography.AesGcm`)
so tampering and wrong-key decryption both fail on the authentication tag. Each
encrypt uses a fresh 12-byte random nonce; `{ nonce, ciphertext, tag }` are
stored as separate columns. No AAD in v1 (single-user, single-purpose payloads);
recorded so a reviewer does not ask why AAD is empty.

- *Alternative: AES-CBC + separate HMAC.* Rejected -- GCM gives integrity in one
  primitive; hand-rolled encrypt-then-MAC is more error-prone.

### D3: Password verification via an encrypted sentinel, not a separate hash

At `SetPassword` the derived key encrypts a fixed known plaintext sentinel
(a constant byte string) with AES-GCM; `{ nonce, ciphertext, tag }` are stored on
the security row. `Unlock`/verify re-derives the key and decrypts the sentinel; a
GCM tag failure is a wrong password. This reuses the token crypto path (one code
path, one algorithm) and -- crucially -- verifies correctly even when zero owner
tokens are stored yet (the window right after first-run SetPassword).

- *Alternative: a second independent KDF/hash of the password as verifier.*
  Rejected -- adds a second algorithm and code path for no gain here; the sentinel
  already provides zero-token verification.

### D4: Two ports -- `ITokenVault` (data) and `IAppLock` (session/key)

The decrypted-key/session state is separated from data-at-rest operations.
`IAppLock` (Core) owns `UnlockAsync`, `CurrentState`, and exposes whether a key
is held; `ITokenVault` keeps `StoreTokenAsync`/`GetTokenAsync` and gains
`SetPasswordAsync` and `ResetVaultAsync`. `SetPassword`/`Reset` sit on the vault
(they write the security/token rows) rather than the lock; the lock only holds
and derives. This keeps each port cohesive and within the parameter/complexity
limits, and lets #5 depend on `IAppLock` alone to gate polling.

- *Alternative: fold everything into `ITokenVault`.* Rejected -- conflates
  transient session state with persistent storage and bloats one interface.

### D5: The decrypted key lives in a singleton holder; the vault is scoped

The key must be shared across Blazor circuits/tabs and survive between scoped
requests, so an `IVaultKeyHolder` (or the `IAppLock` implementation) is registered
**singleton** and carries the 32-byte key plus the derived lock state. `TokenVault`
stays **scoped** (it depends on the scoped `PrCenterDbContext`) and reads the key
from the singleton holder at call time. `AddPersistenceAdapter` registration
splits accordingly: `AddSingleton` for the `VaultKeyHolder`, `AddScoped` for both
`IAppLock` and the vault (both need the scoped `DbContext`). The holder is
`IDisposable`, so on process stop the container disposes it and its `Dispose`
zeroes the key (D-lock: Locked on next start). The key is stored in a plain
`byte[]`; clearing on reset, on re-key, and on dispose zeroes it, and
`GetKeyOrThrow` hands callers an ephemeral copy so an in-place clear cannot
corrupt crypto already in flight.

- *Alternative: scoped key holder.* Rejected -- a scoped holder cannot share the
  key across circuits or persist it between requests, defeating "unlock once."

### D6: Two EF entities + one migration, owned here

`OwnerToken { Owner (PK, string), Nonce, Ciphertext, Tag (byte[]) }` and
`AppSecurity { Id (single-row PK), Salt, MemoryKib, Iterations, Parallelism,
KdfVersion, SentinelNonce, SentinelCiphertext, SentinelTag }`. Configured with the
Fluent API in `OnModelCreating` (Core stays EF-free, per #3 D1). One migration adds
both tables. `AppSecurity` is a singleton row (fixed PK, e.g. `1`); "a security row
exists" is the Uninitialized/Locked discriminator. Reads that only check existence
or read the security row use `AsNoTracking` + `Select` per the baseline; the upsert
paths track.

### D7: Locked-access is defense in depth, not the primary gate

`StoreTokenAsync`/`GetTokenAsync` throw `VaultLockedException` when no key is held,
even though #5 gates polling on `IAppLock` upstream. This guards against a caller
that forgets to check, and makes the "no data while Locked" state-doc invariant
enforceable at the vault boundary. `SetPasswordAsync` does not require unlock (it
establishes the key); `ResetVaultAsync` does not require unlock (no-recovery wipe).

### D8: Doc sweep in the same change

`docs/pr-center-state.md` section 3 and `docs/pr-center-architecture.md` app-lock
notes currently show two states (`Locked`/`Unlocked`). This change adds the
`Uninitialized` state (no password set yet), so both docs are corrected in this
change per the sweep-corrections convention -- the FSM diagram, the prose, and any
"Locked on start" wording become the three-state model. Idea-doc Key Decisions are
unchanged (they never claimed two states; they describe unlock, reset, no-recovery).

## Risks / Trade-offs

- [Argon2 memory cost on a workstation container] -> parameters are stored per
  row and tuned to the OWASP Argon2id minimum (19456 KiB / t=2 / p=1); #8 sizes
  the container with headroom.
- [`byte[]` key is not pinned/`SecureString`] -> accepted for a single-user
  localhost app; the threat model is at-rest volume theft, not a live memory
  scrape. Zeroed on reset/dispose as best effort.
- [Sentinel is a known plaintext] -> safe for AES-GCM: a known plaintext does not
  weaken the key; it is the standard "verify the key decrypts something" pattern.
- [Two ports where the skeleton had one] -> a small surface change, but `ITokenVault`
  callers (only DI wiring today) are unaffected in signature; the split is additive
  plus the new locked-throw behavior (called out BREAKING in the proposal).

## Migration Plan

- Add the Argon2 package to `Directory.Packages.props`; reference it in
  `PrCenter.Persistence`.
- Add the `OwnerToken`/`AppSecurity` entities and one EF migration; startup
  migration (from #3) applies it while Locked -- schema is not secret.
- Split DI registration (singleton holder/lock, scoped vault).
- Delete the `TokenVaultTests` throw-tests; add crypto/guard/lock tests on the
  #3 real-SQLite harness.
- No rollback data concern: the tables are new and empty until a user sets a
  password; dropping the migration on rollback loses only locally-entered tokens,
  which are re-enterable by design (no-recovery model).

## Open Questions

- None blocking. Exact Argon2 parameter values and the sentinel constant are
  implementation values set during the build; the KDF-version column exists so
  they can change without a data break.
