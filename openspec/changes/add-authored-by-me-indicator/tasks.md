## 1. Core: group the derived flags and add authored-by-me

- [x] 1.1 Add a `QueueItemStatus` sub-record `{ MembershipState State, bool HasUpdate, bool AuthoredByMe }` in `PrCenter.Core`, one type per file, `public sealed record` (part of `QueueItem`'s public constructor), XML-documented.
- [x] 1.2 Refactor `QueueItem` to take `QueueItemStatus` in place of the standalone `state` and `hasUpdate` constructor params (constructor lands at 6 params); keep `State`/`HasUpdate` as flat properties and add flat `AuthoredByMe`, so read-side consumers are unchanged. Update XML docs.
- [x] 1.3 In `QueueItemDeriver`, set `AuthoredByMe` via `GitHubLogin.IsMe(facts.Identity.AuthorLogin, myLogin)`; confirm no membership/`MembershipDeriver` change.

## 2. Core tests

- [x] 2.1 TDD `QueueItemDeriver`: authored-by-me flag true when author login is the user's, false otherwise (covers both `queue-derivation` scenarios).
- [x] 2.2 Confirm the flag is display-only: a self-authored PR still shows/hides by the unchanged membership rules (no new membership behavior).
- [x] 2.3 Update existing `QueueItem`/deriver tests for the `QueueItemStatus` grouping.

## 3. Web: render the indicator

- [x] 3.1 In `QueueRow.razor`, render a distinct text badge ("mine") in the title line when the authored-by-me flag is set, with `data-testid="mine-badge"`, consistent with the existing "Updated"/"covered" badges.
- [x] 3.2 Add styling for the badge; meaning carried by text, not color alone (satisfies the color-alone prohibition).
- [x] 3.3 No read-path change needed (`Item.State`/`Item.HasUpdate` stay flat); `QueueRow`/`InboxView` compile against the refactored `QueueItem` (Web build clean).

## 4. Web tests

- [x] 4.1 bUnit: a self-authored row renders the indicator (covers `review-queue-ui` self-authored scenario).
- [x] 4.2 bUnit: a row authored by another renders no indicator.
- [x] 4.3 Update existing `QueueRow`/`InboxView` bUnit test builders for the `QueueItemStatus` grouping.

## 5. Verify and close

- [x] 5.1 `dotnet build` clean (0 warn / 0 err; param limit respected); CSharpier check passes (179 files).
- [x] 5.2 Full suite green (568 tests); changed Core classes (`QueueItem`, `QueueItemDeriver`, `QueueItemStatus`) at 100% line coverage.
- [x] 5.3 `openspec validate add-authored-by-me-indicator --strict` passes; app confirmed showing the "MINE" badge on self-authored rows and none on others.
