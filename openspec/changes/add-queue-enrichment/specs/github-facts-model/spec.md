# github-facts-model Delta

## MODIFIED Requirements

### Requirement: Pull-request facts are a transport-neutral Core type

`PrCenter.Core` SHALL define a `PullRequestFacts` type (and its sub-records)
carrying exactly the facts the queue derivers read, with no GitHub, EF, or
ASP.NET dependency. It SHALL be the declared return shape of the `IGitHubFacts`
port so the GitHub adapter produces it and the derivers consume it. To keep any
one constructor at or below the baseline parameter limit, the facts SHALL be
grouped into three cohesive sub-records -- identity, status, and activity. The
type SHALL carry:

- **Identity**: a stable pull-request identifier usable as the last-seen marker
  key, plus the owner, repository, number, title, URL, and the author's login
  (the login of whoever opened the pull request, for display).
- **Status**: `IsDraft`, a closed-or-merged indicator, and the last-updated
  author login and instant (for display).
- **Activity**: the set of *directly* requested reviewer logins (team-routed
  requests are out of scope); the list of submitted reviews, each with a
  reviewer login, a review state (approved / changes-requested / commented),
  a submitted timestamp, and an **actor-type flag (`IsBot`)**; and the lists
  of update-worthy events -- commits and comments -- each with an author
  identity and a timestamp, where comments also carry an actor-type flag
  (`IsBot`) and the commit timestamp is the instant the commit landed on the
  branch, not the author date.

Actor-type flags are set from the API's actor type (`__typename`/`user.type`
equals `Bot`), never from login text -- the same bot's login varies by API
surface. Commits carry no actor-type flag: the decided bot policy counts bot
commits, so no deriver reads one. A commit's author identity is the linked
GitHub login when the commit email maps to an account, otherwise the commit
author email or name -- it is always present, but is not guaranteed to be a
login. The pull-request author login is display data only: no deriver reads
it, and it carries no actor-type flag.

#### Scenario: Facts model has no infrastructure dependency

- **WHEN** the architecture tests run
- **THEN** `PullRequestFacts` and its sub-records live in `PrCenter.Core` and
  the existing rule that Core references no GitHub/EF/ASP.NET assemblies still
  holds

#### Scenario: Every update-worthy event carries an author and a timestamp

- **WHEN** a `PullRequestFacts` value is constructed
- **THEN** each commit, comment, and review it holds exposes an author
  identity and a timestamp, so update detection can filter by author and
  instant without any further lookup

#### Scenario: Reviews and comments expose their actor type

- **WHEN** a `ReviewFact` or `CommentFact` is constructed
- **THEN** it exposes an `IsBot` flag the derivers can read without any
  further lookup

#### Scenario: Identity carries the author login

- **WHEN** a `PullRequestIdentity` is constructed
- **THEN** it exposes the pull-request author's login as a required
  (non-null, non-whitespace) string
