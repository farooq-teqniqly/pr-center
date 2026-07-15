# github-adapter Delta

## MODIFIED Requirements

### Requirement: Payload mapping follows the verified rules

The adapter SHALL map GraphQL payloads onto the facts model as follows:

- Review and comment actor type: `IsBot` is true iff the author's
  `__typename` is `Bot`; login text SHALL never be used for bot detection.
- Dismissed reviews (state `DISMISSED`) SHALL be omitted from the facts
  entirely.
- Commit land-date maps from `committedDate`.
- Commit author identity maps from the linked user's login when present,
  otherwise the commit author email, otherwise the commit author name.
- Draft pull requests SHALL be fetched and mapped with `IsDraft` true, not
  excluded by the search query -- exclusion is Core's decision.
- The pull-request author login maps from the already-fetched top-level
  `author { login }`; when the author is null or the login blank (a deleted
  "ghost" account), it SHALL fall back to `"unknown"`, matching the existing
  last-updated-by fallback, never a null or blank value. The GraphQL query is
  unchanged.

#### Scenario: Bot review is marked

- **WHEN** a review's author has `__typename: "Bot"` (e.g. login `Copilot`
  or `qodo-code-review`, which vary by surface)
- **THEN** its `ReviewFact.IsBot` is true

#### Scenario: Dismissed review is omitted

- **WHEN** the payload contains a review with state `DISMISSED`
- **THEN** no `ReviewFact` is produced for it

#### Scenario: Unlinked commit author falls back

- **WHEN** a commit's `author.user` is null (email not linked to a GitHub
  account)
- **THEN** `CommitFact.AuthorLogin` carries the commit author email (or name
  when the email is absent), never a null or blank value

#### Scenario: Pull-request author is mapped

- **WHEN** a pull-request payload carries `author { login }`
- **THEN** the mapped identity's author login is that login

#### Scenario: Ghost author falls back

- **WHEN** a pull-request payload's `author` is null (deleted account)
- **THEN** the mapped identity's author login is `"unknown"`, never null or
  blank
