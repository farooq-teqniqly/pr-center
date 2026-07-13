## ADDED Requirements

### Requirement: App password establishes the vault
The system SHALL let the user set an app password once, deriving the encryption
key with Argon2id from the password and a randomly generated salt, and persisting
only the salt and the Argon2 parameters (memory, iterations, parallelism) plus an
encrypted sentinel. The password itself SHALL NOT be stored in any form.

#### Scenario: First-run password set
- **WHEN** the user sets an app password and no security row exists
- **THEN** the system generates a random salt, derives the key via Argon2id, encrypts a fixed known sentinel under that key with AES-GCM, and stores the salt, the Argon2 parameters, and the encrypted sentinel

#### Scenario: Password cannot be set twice
- **WHEN** the user attempts to set an app password and a security row already exists
- **THEN** the system rejects the request and leaves the existing security row unchanged

### Requirement: Password verification uses the encrypted sentinel
The system SHALL verify an entered password by re-deriving the key with the
stored salt and Argon2 parameters and decrypting the stored sentinel; a failed
AES-GCM authentication tag SHALL mean the password is wrong. Verification SHALL
succeed even when no owner tokens are stored yet.

#### Scenario: Correct password
- **WHEN** the entered password re-derives a key that decrypts the sentinel with a valid authentication tag
- **THEN** the system accepts the password

#### Scenario: Wrong password
- **WHEN** the entered password re-derives a key whose AES-GCM decryption of the sentinel fails the authentication tag
- **THEN** the system rejects the password and no key is retained

#### Scenario: Verification with zero tokens stored
- **WHEN** a password is verified immediately after being set, before any owner token is stored
- **THEN** verification still succeeds or fails purely on the sentinel, never erroring for lack of tokens

### Requirement: Tokens are stored encrypted at rest, one per owner
The system SHALL store at most one fine-grained PAT per owner, encrypted with
AES-GCM under the derived key, with a per-token random nonce and the
authentication tag persisted alongside the ciphertext. Storing a token for an
owner that already has one SHALL replace it.

#### Scenario: Store a new owner token
- **WHEN** a token is stored for an owner while the vault is unlocked
- **THEN** the system encrypts it with AES-GCM under the derived key using a fresh random nonce and persists ciphertext, nonce, and tag keyed by owner

#### Scenario: Replace an existing owner token
- **WHEN** a token is stored for an owner that already has a stored token
- **THEN** the system replaces the existing ciphertext, nonce, and tag for that owner

#### Scenario: Plaintext never persisted
- **WHEN** a token is stored
- **THEN** the plaintext token SHALL NOT appear in any persisted column

### Requirement: Token retrieval decrypts under the in-memory key
The system SHALL return the decrypted PAT for an owner while the vault is
unlocked, or null when no token is stored for that owner.

#### Scenario: Retrieve a stored token
- **WHEN** a token is requested for an owner that has one and the vault is unlocked
- **THEN** the system decrypts and returns the plaintext PAT

#### Scenario: Retrieve when none stored
- **WHEN** a token is requested for an owner that has no stored token and the vault is unlocked
- **THEN** the system returns null

### Requirement: Token access is refused while locked
The system SHALL refuse to store or retrieve tokens when the vault is not
unlocked, throwing a locked-vault error rather than returning data or a null.

#### Scenario: Store while locked
- **WHEN** a token store is attempted while the vault is Locked or Uninitialized
- **THEN** the system throws a locked-vault error and persists nothing

#### Scenario: Retrieve while locked
- **WHEN** a token retrieval is attempted while the vault is Locked or Uninitialized
- **THEN** the system throws a locked-vault error and returns no data

### Requirement: Reset wipes all vault data with no recovery
The system SHALL provide a reset that deletes every stored owner token and the
security row (salt, Argon2 parameters, sentinel), returning the vault to its
uninitialized condition. Reset SHALL NOT require the password and SHALL have no
recovery path.

#### Scenario: Reset clears tokens and security
- **WHEN** the user resets the vault
- **THEN** the system deletes all token rows and the security row, and a subsequent password set is treated as first-run

#### Scenario: Reset does not need unlock
- **WHEN** the user resets the vault while it is Locked
- **THEN** the reset succeeds without requiring the password
