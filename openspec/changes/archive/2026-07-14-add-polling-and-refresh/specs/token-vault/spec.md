## ADDED Requirements

### Requirement: Vault enumerates owners with a stored token
The system SHALL enumerate the owners that currently have a stored token. This
enumeration is the authoritative owner list for polling. It SHALL read only the
plaintext owner key column, SHALL NOT decrypt any token, and SHALL work
regardless of lock state (lock gating of polling is the app lock's concern, not
the vault's).

#### Scenario: Owners with tokens are listed
- **WHEN** tokens are stored for one or more owners and the owner list is requested
- **THEN** the system returns exactly the owners that have a stored token

#### Scenario: Empty vault lists no owners
- **WHEN** no owner tokens are stored and the owner list is requested
- **THEN** the system returns an empty list

#### Scenario: Enumeration works while locked
- **WHEN** the owner list is requested while the vault is Locked or Uninitialized
- **THEN** the system returns the stored owners without error and without decrypting anything
