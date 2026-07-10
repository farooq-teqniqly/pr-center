# github-facts-model

## ADDED Requirements

### Requirement: Pull-request facts are a transport-neutral Core type

`PrCenter.Core` SHALL define a `PullRequestFacts` type (and its sub-records)
carrying exactly the facts the queue derivers read, with no GitHub, EF, or
ASP.NET dependency. It SHALL be the declared return shape of the `IGitHubFacts`
port so the GitHub adapter produces it and the derivers consume it. The type
SHALL carry:

- A stable pull-request identifier usable as the last-seen marker key, plus the
  owner, repository, number, title, and URL.
- `IsDraft` and a closed-or-merged indicator.
- The set of *directly* requested reviewer logins (team-routed requests are out
  of scope).
- The list of submitted reviews, each with a reviewer login, a review state
  (approved / changes-requested / commented), and a submitted timestamp.
- The lists of update-worthy events -- commits and comments -- each with an
  author login and a timestamp; the commit timestamp is the instant the commit
  landed on the branch, not the author date.

#### Scenario: Facts model has no infrastructure dependency

- **WHEN** the architecture tests run
- **THEN** `PullRequestFacts` and its sub-records live in `PrCenter.Core` and
  the existing rule that Core references no GitHub/EF/ASP.NET assemblies still
  holds

#### Scenario: Every update-worthy event carries an author and a timestamp

- **WHEN** a `PullRequestFacts` value is constructed
- **THEN** each commit, comment, and review it holds exposes an author login
  and a timestamp, so update detection can filter by author and instant without
  any further lookup

### Requirement: Facts are pure data with guarded construction

`PullRequestFacts` and its sub-records SHALL be immutable data carriers with no
derivation behavior. Reference-type constructor parameters SHALL be null-guarded
per the baseline; required strings SHALL reject null-or-whitespace.

#### Scenario: Null required fact is rejected

- **WHEN** a `PullRequestFacts` (or sub-record) is constructed with a null
  reference-type argument that the contract requires
- **THEN** construction throws `ArgumentNullException` (or
  `ArgumentException` for a null-or-whitespace required string)
