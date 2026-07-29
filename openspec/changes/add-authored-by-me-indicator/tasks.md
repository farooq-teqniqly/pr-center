## 1. Core: group the derived flags and add authored-by-me

- [x] 1.1 Add a `QueueItemStatus` sub-record `{ MembershipState State, bool HasUpdate, bool AuthoredByMe }` in `PrCenter.Core`, one type per file, `public sealed record` (part of `QueueItem`'s public constructor), XML-documented.
- [x] 1.2 Refactor `QueueItem` to take `QueueItemStatus` in place of the standalone `state` and `hasUpdate` constructor params (constructor lands at 6 params); keep `State`/`HasUpdate` as flat properties and add flat `AuthoredByMe`, so read-side consumers are unchanged. Update XML docs.
- [x] 1.3 In `QueueItemDeriver`, set `AuthoredByMe` via `GitHubLogin.IsMe(facts.Identity.AuthorLogin, myLogin)`; confirm no membership/`MembershipDeriver` change.

## 2. Core tests

- [x] 2.1 TDD `QueueItemDeriver`: authored-by-me flag true when author login is the user's, false otherwise (covers both `queue-derivation` scenarios).
- [x] 2.2 Confirm the flag is display-only: a self-authored PR still shows/hides by the unchanged membership rules (no new membership behavior).
- [x] 2.3 Update existing `QueueItem`/deriver tests for the `QueueItemStatus` grouping.

## 3. Web: render the indicator

- [ ] 3.1 In `QueueRow.razor`, render a distinct text badge (e.g. "mine") in the title line when the authored-by-me flag is set, with a `data-testid`, consistent with the existing "Updated"/"covered" badges.
- [ ] 3.2 Add styling for the badge; meaning carried by text, not color alone (satisfies the color-alone prohibition).
- [ ] 3.3 No read-path change needed (`Item.State`/`Item.HasUpdate` stay flat); verify `QueueRow`/`InboxView` still compile against the refactored `QueueItem`.

## 4. Web tests

- [ ] 4.1 bUnit: a self-authored row renders the indicator (covers `review-queue-ui` self-authored scenario).
- [ ] 4.2 bUnit: a row authored by another renders no indicator.
- [ ] 4.3 Update existing `QueueRow` bUnit tests for the `QueueItemStatus` grouping.

## 5. Verify and close

- [ ] 5.1 `dotnet build` clean (no new analyzer/CA warnings; param limit respected); CSharpier check passes.
- [ ] 5.2 Run affected test projects (Core + Web) green; coverage per CLAUDE.md workflow.
- [ ] 5.3 `openspec validate add-authored-by-me-indicator --strict` passes; run the app and confirm the indicator on a self-authored inbox row.
