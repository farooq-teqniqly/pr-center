# PR-Center conventions

Project context and conventions for this repo. See [`docs/pr-center-idea.md`](docs/pr-center-idea.md)
for the product concept, [`docs/pr-center-state.md`](docs/pr-center-state.md) for the derived
state machines, [`docs/pr-center-architecture.md`](docs/pr-center-architecture.md) for the
project boundaries and dependency direction (prose mirror of the canonical Lucid diagram),
and [`docs/pr-center-roadmap.md`](docs/pr-center-roadmap.md) for the planned sequence of
OpenSpec changes.

Shared .NET conventions are imported from the baseline below (source of truth:
[farooq-teqniqly/claude-templates](https://github.com/farooq-teqniqly/claude-templates)).
Rules in this file are project-specific and override the baseline where they conflict.

@CLAUDE-baseline.md

## What this is

A single-user, self-hosted "review inbox" for GitHub PRs awaiting the user's review across
multiple orgs (PerfectServe, ps-unite) and a personal account (farooq-teqniqly). Read-only
projection of GitHub state: it never mutates PRs. Runs in Podman/Docker on a workstation.

## Stack

- **Backend:** ASP.NET Core (C#/.NET), hitting the GitHub API directly (REST/GraphQL) — no `gh` CLI dependency.
- **Frontend:** Blazor Server (interactive C# components; SignalR circuit is the render transport, not a chosen freshness mechanism).
- **State:** local SQLite/JSON file on a mounted host volume. No external database. (Baseline
  override: this project uses SQLite, not SQL Server; integration tests exercise the real
  SQLite file, so Testcontainers is not required for the database.)
- **Auth to GitHub:** one fine-grained PAT per owner, entered in-app, encrypted at rest with an app-password-derived key.

## Design invariants

Do not violate these without updating the idea/state docs first:

- **All PR state is evaluated relative to the user only.** Another reviewer's activity never removes a PR from the list; the user's own commits/comments/reviews never flip the "has an update" indicator.
- **Membership is derived each poll** as a pure function of current GitHub facts — no stored transition FSM.
- **"Has an update"** = new commits/pushes, new comments/replies, or a new review by another reviewer since last looked. A bare `updatedAt` bump does not count. Bot/CI comments and reviews do not count either; bot commits do. Bot detection is by API actor type, never login text.
- **"Already covered"** counts only other humans' non-dismissed reviews — bot reviews never make a PR covered.
- **Draft PRs are excluded** entirely, even when the user is a requested reviewer.
- **Mark-as-seen** happens on click-through, via a fresh live fetch of that PR (not the last poll snapshot).
- **Never mutate PR state** (no approve/comment/request-changes from the app).

## Code conventions (beyond baseline)

- **Constructor and method parameter count stays at 7 or below** (S107; SonarCloud flags 8+),
  reinforcing the baseline rule. This applies to data carriers too: when a record or DTO would
  exceed the limit, do not pass a flat parameter list -- group the fields into cohesive
  sub-records that reflect genuine domain concepts (e.g. `PullRequestFacts` groups its fields
  into `PullRequestIdentity` / `PullRequestStatus` / `PullRequestActivity`), each itself within
  the limit and null-guarded.
- **Favor domain expressiveness during implementation, not just at review.** Code should read
  as intent in the language of the domain. Prefer intention-revealing names over mechanical
  ones (`GitHubLogin.IsMe` / `NotMe` over `AreSame` plus a `!`), extract buried mechanics into
  named helpers so the caller reads as a sentence (`LatestReviewBy(facts, myLogin)` over inline
  LINQ; `UpdateEvents(activity)` over three OR-ed `.Any(...)` chains), and name booleans and
  states for the concept they represent (`AwaitingReReview`, not a flag combination). This is a
  first-pass obligation while writing the code, not a cleanup deferred to PR review.

## Writing style (beyond baseline)

- Do not silently rewrite existing British spellings in unrelated files; flag inconsistencies in files you are already editing.

## Commits (beyond baseline)

- The `commit-msg` hook lives in `.githooks/` (see [README](README.md#commit-message-format)).

## PowerShell authoring rules

Scripts under `docs/` (e.g. `Get-PRQueue.ps1`) target Windows PowerShell 5.1.

- **No em dashes or smart quotes in string literals.** 5.1 reads UTF-8-without-BOM as Windows-1252; em dash (—) and curly quotes corrupt and break parsing. Use `--` or `-`, and straight `"` / `'` only.
- **`param()` must be the very first statement** in any parameterized script.
