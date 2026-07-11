# Tasks: add-github-adapter

TDD for all production behavior: failing test first, confirm red, minimum
code to green, refactor. Facts and deriver work lands in `PrCenter.Core` +
`PrCenter.Core.Tests`; adapter work in `PrCenter.GitHub` +
`PrCenter.GitHub.Tests` (fake `HttpClient` handler, fixtures shaped from the
spike's real payloads -- no live API calls); DI wiring in `PrCenter.Web` +
`PrCenter.Web.Tests`.

## 1. Facts model: actor type

- [x] 1.1 Tests first: `ReviewFact` and `CommentFact` expose an `IsBot` flag that round-trips from the constructor
- [x] 1.2 Add `IsBot` to both records (additive constructor parameter; existing guards unchanged); update XML docs
- [x] 1.3 Update `CommitFact.AuthorLogin` XML doc to the author-identity semantics (login when linked, else email or name -- always present, not guaranteed to be a login); no shape change
- [x] 1.4 Update `TestFacts`/`TestLogins` helpers as needed; all existing Core tests stay green

## 2. Deriver amendments: bot policy

- [x] 2.1 Tests first for `UpdateDetector`: bot comment/review after marker -> no update; bot commit after marker -> update; human comment/review after marker -> update (unchanged)
- [x] 2.2 Implement the bot filter in `UpdateDetector` (comments and reviews only; commits never filtered) to green
- [x] 2.3 Tests first for `CoveredFlag`: only bot reviews by others -> not covered; human review by another -> covered (unchanged)
- [x] 2.4 Implement the human-only filter in `CoveredFlag` to green
- [x] 2.5 Confirm `MembershipDeriver` needs no change (its tests stay green untouched)

## 3. Core port surface

- [x] 3.1 Define `OwnerFetchStatus` (`Ok | MisconfiguredToken | Error`) and `OwnerFactsResult` (status + facts list + optional detail), null-guarded, with guard tests
- [x] 3.2 Extend `IGitHubFacts` with `GetReviewQueueFactsAsync(owner, myLogin, ct)` and `GetPullRequestFactsAsync(owner, repository, number, ct)`; XML docs per D1 (single-PR returns null only when inaccessible/gone; closed/merged still returns facts)
- [x] 3.3 Architecture tests stay green (new types in Core only)

## 4. Adapter: infrastructure and mapping

- [x] 4.1 Add the fake-`HttpClient` test handler (baseline pattern: public abstract `MockSendAsync`, sealed `SendAsync`) and fixture files shaped from the spike payloads (include: null commit `author.user`, `__typename: "Bot"` review/comment authors, a `DISMISSED` review, an `isDraft: true` PR)
- [x] 4.2 Tests first: GraphQL response mapping onto `PullRequestFacts` -- bot flags set from `__typename` only, dismissed reviews omitted, `committedDate` -> `LandedAt`, author fallback login -> email -> name, draft PRs mapped with `IsDraft` true
- [x] 4.3 Implement the query document (two aliased searches per D3, nested selection, page sizes/caps) and the response mapping to green
- [x] 4.4 Tests first: discovery union -- PR present in both searches maps to exactly one facts instance; re-review-only PR (reviewed-by hit) is included
- [x] 4.5 Implement union/dedupe by PR id to green

## 5. Adapter: auth and status

- [ ] 5.1 Tests first: token flows from a faked `ITokenVault` for exactly the requested owner into the `Authorization` header; `User-Agent` present; token never appears in any `OwnerFactsResult` detail or log output
- [ ] 5.2 Implement vault-sourced auth (no adapter-side token caching) to green
- [ ] 5.3 Tests first: status mapping -- 401/FORBIDDEN -> `MisconfiguredToken`; rate-limit exhaustion -> `Error` with detail; network failure/5xx/malformed payload -> `Error`; 200 with zero results -> `Ok` with empty list; no exception escapes `GetReviewQueueFactsAsync` for any fetch failure
- [ ] 5.4 Implement the error-to-status mapping (D9) to green
- [ ] 5.5 Null/whitespace guard tests for every new public/internal member (issue #6 GitHub half), guards documented with `<exception>` tags

## 6. Adapter: remaining members

- [ ] 6.1 Tests first: `GetPullRequestFactsAsync` -- merged PR returns facts with `IsClosedOrMerged` true; missing/inaccessible PR returns null
- [ ] 6.2 Implement the single-PR query and mapping to green
- [ ] 6.3 Tests first (replacing the stub test): `GetAuthenticatedUserLoginAsync` returns the viewer login via GraphQL; delete `GetAuthenticatedUserLoginAsync_WhenCalled_ThrowsNotImplemented` in the same commit (issue #6: never port stub tests forward)
- [ ] 6.4 Implement the viewer query to green

## 7. DI wiring

- [ ] 7.1 Add `Microsoft.Extensions.Http.Resilience` to `Directory.Packages.props` (CPM) with the package reference in `PrCenter.Web`
- [ ] 7.2 Register the named `HttpClient` (`IHttpClientFactory`) with `AddStandardResilienceHandler()` (defaults per D4a) and the real `GitHubFactsClient` in `PrCenter.Web`'s composition root; DI resolution test updated/extended
- [ ] 7.3 Architecture tests still green (GitHub adapter references Core only; no Persistence reference crept in; resilience package referenced by Web only)

## 8. Wrap-up

- [ ] 8.1 `dotnet build` clean (warnings-as-errors) and `dotnet test` green across all projects
- [ ] 8.2 `dotnet csharpier check .` clean
- [ ] 8.3 Core statement coverage stays at 100%; adapter coverage reviewed (fixtures should exercise every mapping branch)
- [ ] 8.4 Comment on issue #6: GitHub half done (stub test deleted, guards + guard tests landed); Persistence half remains for #3/#4 -- do not close the issue
