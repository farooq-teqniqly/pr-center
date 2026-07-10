# Proposal: add-queue-derivation

## Why

The solution skeleton (`add-solution-architecture`) defined the ports but no
behavior. The heart of PR-Center is the pure logic that turns raw GitHub facts
into "does this PR show, is it flagged updated, is it already covered" -- all
evaluated relative to the user, recomputed each poll with no stored transition
history (see [pr-center-state.md](../../../docs/pr-center-state.md) and the
architecture in [pr-center-architecture.md](../../../docs/pr-center-architecture.md)).
This is the heaviest TDD payoff in the project and has no I/O, so it can land
in parallel with the GitHub adapter (`add-github-adapter`) and the state store
(`add-state-store`), each of which binds to the fact model this change defines.

## What Changes

- Define the **GitHub facts model** in `PrCenter.Core`: `PullRequestFacts` plus
  its sub-records (reviews, review-request roster, and update-worthy events --
  commits and comments -- each carrying an author login and a timestamp). Pure
  data, no behavior. This is Core-owned contract surface: the return shape of
  `IGitHubFacts`, produced by the GitHub adapter (#2) and consumed by the
  derivers and by `RefreshQueue` (#5).
- Implement three pure derivers in `PrCenter.Core`, TDD:
  - `MembershipDeriver` -- decides whether a PR is shown and in which shown
    state (`AwaitingFirstReview` / `AwaitingReReview`) or hidden and why, all
    relative to the user. Encodes draft exclusion, closed/merged drop, and the
    "latest review verdict" rule.
  - `UpdateDetector` -- given a last-seen marker, returns whether the PR has an
    update: any other person's commit/comment/review with a timestamp after
    the marker. The user's own activity and bare `updatedAt` bumps never count.
  - `CoveredFlag` -- true when at least one other reviewer has submitted any
    review (pending requests do not count).
- Define the `QueueItem` output type: PR identity plus the derived
  membership state, has-update flag, and already-covered flag.

## Non-goals

- **No I/O.** No `IGitHubFacts` implementation, no `IStateStore` reads/writes,
  no HTTP. The derivers take plain facts and a marker value as inputs.
- **No `RefreshQueue` / `GetQueue` / `MarkSeen`.** Those orchestrate fetch and
  persistence; they belong to `add-polling-and-refresh` (#5) and wire these
  derivers to the ports.
- **No sorting or grouping.** Unseen-first / most-recently-updated ordering and
  org/repo grouping are display concerns in `add-review-queue-ui` (#6).
- No extension of the `IGitHubFacts` port method set beyond returning the facts
  model type; the adapter change owns the fetch surface.

## Capabilities

### New Capabilities

- `github-facts-model`: the Core-owned `PullRequestFacts` record and its
  sub-records -- the transport-neutral shape every layer agrees on for a PR's
  reviewable facts.
- `queue-derivation`: the three pure derivers and the `QueueItem` output,
  encoding the membership, seen/updated, and already-covered rules from the
  state doc.

### Modified Capabilities

*None -- the skeleton's `solution-structure` spec is unaffected; this change
adds behavior behind the existing `IGitHubFacts` seam.*

## Impact

- New pure types and classes in `PrCenter.Core`; unit tests in
  `PrCenter.Core.Tests`. No adapter or Web changes.
- `add-github-adapter` (#2) will map GitHub API responses onto this fact model.
- `add-polling-and-refresh` (#5) will call the derivers with fetched facts and
  the persisted marker.
- One inferred GitHub semantic is recorded as an assumption to verify in #2
  (see design.md): submitting a review clears the user from the requested
  reviewers, so a present review request always means a fresh ask.
