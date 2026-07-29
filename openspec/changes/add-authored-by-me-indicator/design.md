## Context

The review inbox shows self-authored pull requests when the user is otherwise
engaged (e.g. commented on their own PR), but no row element distinguishes a PR
the user opened from one opened by someone else. Issue #40 asks for a marker so
the user can skip rows they never intend to review.

The row already carries the author: `PullRequestIdentity.AuthorLogin`
(`QueueItem.Identity`). The UI does not carry a raw `myLogin` at row level --
"me-ness" is precomputed in Core and exposed as booleans (`ReviewerRosterEntry.IsMe`),
which the razor reads directly (`RosterChips.razor`).

## Goals / Non-Goals

**Goals:**
- A distinct, non-color-only per-row indicator when the row's author is the user.
- Follow the established precompute-me-ness-in-Core pattern.

**Non-Goals:**
- No change to membership (which PRs show or hide). Self-authored PRs appear and
  hide exactly as today.
- No exclusion, deprioritization, or reordering of self-authored PRs. Issue #40
  asks for an indicator only.
- No new GitHub fact or field -- `AuthorLogin` already ships.

## Decisions

### Compute `AuthoredByMe` in Core, not in the UI
Add an additive `AuthoredByMe` bool to `QueueItem`, set by `QueueItemDeriver`
via `GitHubLogin.IsMe(identity.AuthorLogin, myLogin)`. The razor reads the bool.

- **Why:** mirrors `ReviewerRosterEntry.IsMe`, the existing pattern for surfacing
  me-ness to the UI. Keeps the login comparison (and its `StringComparison`
  correctness) in one tested place in Core.
- **Alternative (rejected):** plumb `myLogin` into `QueueRow` and compare in the
  razor. Zero Core edit, but introduces a second, untested me-comparison path in
  the view and breaks the one-place pattern. `QueueRow` has no `myLogin` today.

### Group the per-row derived flags to stay within the param limit
`QueueItem`'s constructor is already at the 7-parameter ceiling (S107):
`identity, lastUpdate, state, hasUpdate, roster, myEngagement, coveredBy`. Adding
`AuthoredByMe` as an eighth parameter would exceed it. Group the two standalone
scalar flags plus the membership state into one cohesive `QueueItemStatus`
sub-record `{ MembershipState State, bool HasUpdate, bool AuthoredByMe }`, so the
constructor becomes `identity, lastUpdate, status, roster, myEngagement,
coveredBy` (6 params, with headroom).

- **Why:** matches the project rule for over-limit carriers -- group into a
  sub-record reflecting a genuine domain concept ("the row's derived status")
  rather than a flat list, exactly as `PullRequestFacts` groups into
  `PullRequestStatus` etc. `State` and `HasUpdate` are already derived per-row
  outputs, so they belong with `AuthoredByMe`.
- **Trade-off:** callers move from `QueueItem.State` / `.HasUpdate` to
  `.Status.State` / `.Status.HasUpdate` (razor + tests). Bounded churn; the
  alternative of a flat 8th param is disallowed by S107.
- **Alternative (rejected):** add `AuthoredByMe` to `PullRequestIdentity` -- it
  is also already at 7 fields, and authored-by-me is a me-relative derivation,
  not pure PR identity data.

### Render as a text badge in the title line
Render a short text badge (e.g. "mine") in the row's `title-line`, alongside the
existing "Updated" / "covered" badges, with a `data-testid` for tests.

- **Why:** consistent with sibling badges; text carries the meaning, satisfying
  the spec's "state SHALL never be conveyed by color alone" rule. No new layout
  region.
- **Alternative (rejected):** color-only accent on the row -- violates the
  color-alone prohibition and is easy to miss.

## Risks / Trade-offs

- [`AuthoredByMe` would push `QueueItem` past the 7-param S107 ceiling] ->
  grouped `state`+`hasUpdate`+`authoredByMe` into a `QueueItemStatus` sub-record
  (see Decisions), landing the constructor at 6 params.
- [A row can be both "mine" and "Updated"] -> both badges render; they are
  orthogonal (authorship vs unseen activity). No interaction to resolve.

## Migration Plan

Not applicable. Additive projected field + presentation; no schema, data, or
persisted-state change. Rollback is reverting the change.

## Open Questions

None.
