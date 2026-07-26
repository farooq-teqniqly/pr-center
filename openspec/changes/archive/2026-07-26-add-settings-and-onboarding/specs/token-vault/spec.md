# token-vault Specification

## ADDED Requirements

### Requirement: Tokens can be deleted, one owner at a time
The system SHALL delete the stored token for a single owner while the vault is
unlocked, removing that owner from the enumerated owner list and therefore from
the polled set. Deleting an owner that has no stored token SHALL succeed without
error and SHALL leave other owners untouched. Deletion SHALL affect only the
named owner -- it is not a reset.

#### Scenario: Delete an owner's token
- **WHEN** a delete is requested for an owner that has a stored token and the vault is unlocked
- **THEN** that owner's token row is removed and the owner no longer appears in the enumerated owner list

#### Scenario: Delete leaves other owners intact
- **WHEN** one owner's token is deleted while other owners have stored tokens
- **THEN** the other owners' tokens and the security row are unchanged

#### Scenario: Delete an owner with no token
- **WHEN** a delete is requested for an owner that has no stored token and the vault is unlocked
- **THEN** the operation succeeds without error and nothing is removed

### Requirement: A token row records when it was saved
The system SHALL record the instant a token was stored on that owner's token
row, set on every store including a replacement. The saved instant SHALL be
readable without decrypting the token. A token row written before saved-instant
recording SHALL read as having no recorded instant, and the system SHALL NOT
substitute a fabricated value for it. The system SHALL NOT store any masked
value, fingerprint, or other derivative of the token.

#### Scenario: Storing a token records the instant
- **WHEN** a token is stored for an owner
- **THEN** that owner's row carries the instant of the store

#### Scenario: Replacing a token updates the instant
- **WHEN** a token is stored for an owner that already has one
- **THEN** that owner's recorded instant is updated to the instant of the replacement

#### Scenario: A pre-existing row has no recorded instant
- **WHEN** a token row written before saved-instant recording is read
- **THEN** the saved instant reads as absent rather than as a substituted value

#### Scenario: No token derivative is persisted
- **WHEN** a token is stored
- **THEN** no masked value, prefix, or fingerprint derived from the token appears in any persisted column

## MODIFIED Requirements

### Requirement: Token access is refused while locked
The system SHALL refuse to store, retrieve, or delete tokens when the vault is
not unlocked, throwing a locked-vault error rather than returning data, deleting
a row, or returning a null.

#### Scenario: Store while locked
- **WHEN** a token store is attempted while the vault is Locked or Uninitialized
- **THEN** the system throws a locked-vault error and persists nothing

#### Scenario: Retrieve while locked
- **WHEN** a token retrieval is attempted while the vault is Locked or Uninitialized
- **THEN** the system throws a locked-vault error and returns no data

#### Scenario: Delete while locked
- **WHEN** a token delete is attempted while the vault is Locked or Uninitialized
- **THEN** the system throws a locked-vault error and removes nothing
