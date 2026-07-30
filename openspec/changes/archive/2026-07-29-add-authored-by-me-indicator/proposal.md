## Why

Self-authored pull requests appear in the review inbox (e.g. when the user has
commented on their own PR), but nothing on the row signals that the user opened
it. The user reviews their own code before opening a PR and will not re-review
it, so an at-a-glance "authored by me" marker saves them from opening rows they
never intend to act on. Tracked as issue #40.

## What Changes

- Review inbox rows whose author is the user render a distinct, non-color-only
  "authored by me" indicator (chip/marker). Rows authored by anyone else render
  no such indicator.
- A new additive `AuthoredByMe` flag on `QueueItem`, set by the deriver via
  `GitHubLogin.IsMe(AuthorLogin, myLogin)`, mirroring the existing
  precompute-me-ness pattern on `ReviewerRosterEntry.IsMe`.
- No change to which pull requests appear or hide, to update/covered badges, or
  to any *membership* logic. Self-authored PRs continue to appear and hide
  exactly as today -- this only adds a flag and a marker to rows already shown.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `review-queue-ui`: adds a per-row requirement that a row authored by the user
  shows a distinct, non-color-only authored-by-me indicator, and a row authored
  by anyone else does not. Presentation-only; no other row element changes.
- `queue-derivation`: `QueueItem` gains an additive `AuthoredByMe` flag,
  computed from the existing `AuthorLogin` vs the user's login. Purely a new
  projected field -- membership (which PRs show/hide) is unchanged.

## Impact

- **Code:** `PrCenter.Core` -- add `AuthoredByMe` to `QueueItem`, set it in
  `QueueItemDeriver` via `GitHubLogin.IsMe(AuthorLogin, myLogin)`. `PrCenter.Web`
  -- render the indicator on rows where the flag is set. Mirrors the existing
  `ReviewerRosterEntry.IsMe` precompute pattern.
- **No change:** `MembershipDeriver` (membership unchanged), facts,
  `PrCenter.GitHub`, `PrCenter.Persistence`. `AuthorLogin` already ships on
  `PullRequestIdentity`, so no new fact is needed.
- **Docs/specs:** deltas on `review-queue-ui` and `queue-derivation`. No
  idea/state/architecture doc change -- no design invariant is touched.
