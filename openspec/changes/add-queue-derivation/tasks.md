# Tasks: add-queue-derivation

All production behavior is TDD: write the failing test first, confirm it fails
for the right reason, then the minimum code to pass. Everything lands in
`PrCenter.Core` with tests in `PrCenter.Core.Tests`. No I/O, no adapter or Web
changes.

## 1. GitHub facts model

- [x] 1.1 Define review-state representation (approved / changes-requested / commented) and the review sub-record (reviewer login, state, submitted timestamp)
- [x] 1.2 Define the update-worthy event sub-records: commit (author login, land timestamp) and comment (author login, timestamp)
- [x] 1.3 Define `PullRequestFacts` grouped into cohesive sub-records to stay within the parameter limit: `PullRequestIdentity` (stable id, owner, repo, number, title, url), `PullRequestStatus` (`IsDraft`, closed-or-merged indicator, last-updated author + instant), and `PullRequestActivity` (directly-requested reviewer logins, reviews, commits, comments) -- all immutable, null-guarded data carriers
- [x] 1.4 Test: constructing a fact record/sub-record with a null required reference (or null-or-whitespace required string) throws
- [x] 1.5 Confirm the existing architecture test still passes: the new types keep `PrCenter.Core` free of GitHub/EF/ASP.NET references

## 2. Membership deriver

- [x] 2.1 Define the membership outcome type: shown states (`AwaitingFirstReview`, `AwaitingReReview`) and hidden reasons (draft, closed/merged, approved, untracked)
- [x] 2.2 Tests first for each row of the D2 table plus both early-outs (draft-while-requested excluded; closed/merged dropped)
- [x] 2.3 Implement `MembershipDeriver` (pure; inputs `PullRequestFacts` + `myLogin`) to green; `myLatest` = user's review with greatest submitted timestamp
- [x] 2.4 Test the re-request-after-approval row explicitly (approved latest + amRequested -> AwaitingFirstReview), carrying the assumption noted in design Open Question 1

## 3. Update detector

- [x] 3.1 Tests first: null marker -> update; other-author commit/comment/review after marker -> update; own-only activity after marker -> no update; other activity at/before marker -> no update
- [x] 3.2 Implement `UpdateDetector` (pure; inputs facts + `myLogin` + `DateTimeOffset?` marker) to green using strict `> marker` and `author != myLogin`

## 4. Covered flag

- [ ] 4.1 Tests first: other reviewer's review -> covered; only pending requests -> not covered; only own reviews -> not covered
- [ ] 4.2 Implement `CoveredFlag` (pure; inputs facts + `myLogin`) to green

## 5. Queue item

- [ ] 5.1 Define `QueueItem` (identity + membership state + has-update + already-covered)
- [ ] 5.2 Tests: a shown PR yields a `QueueItem` with the three derived values; a hidden PR yields none; no sorting/grouping is applied
- [ ] 5.3 Implement the assembly step that maps a shown membership result plus the two flags into a `QueueItem` (pure; no ordering)

## 6. Wrap-up

- [ ] 6.1 `dotnet build` clean (warnings-as-errors) and `dotnet test` green
- [ ] 6.2 `dotnet csharpier check .` clean
- [ ] 6.3 Confirm no `IStateStore`/`IGitHubFacts` implementation or call crept in (derivers stay pure); leave the Open Questions in design.md for #2 to resolve
