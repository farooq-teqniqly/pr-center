# Tasks: add-queue-enrichment

## 1. Author fact (github-facts-model + github-adapter)

- [x] 1.1 Red: `PullRequestIdentity` guard test -- null/whitespace author login
      rejected; construction test that the author login is exposed
- [x] 1.2 Green: add `AuthorLogin` to `PullRequestIdentity` (constructor at 7
      parameters, guarded, XML-documented); fix all existing construction
      sites (mapper, tests) to compile
- [x] 1.3 Red: mapper tests -- `author { login }` maps to the identity's
      author login; null author (ghost) falls back to `"unknown"`
- [x] 1.4 Green: map the top-level author in `PullRequestFactsMapper`,
      reusing/extracting the existing author-login read so the updated-by
      fallback and the identity share one helper
- [x] 1.5 Confirm architecture tests still pass (facts stay
      infrastructure-free)

## 2. Roster and covered-by derivation (queue-derivation)

- [ ] 2.1 Red: `ReviewerRosterEntry`/roster deriver tests -- requested-only is
      Pending with `IsBot` false; latest review's state wins for multiple
      reviews; requested-and-reviewed appears once with the review state; bot
      reviewer kept and flagged; is-me flag set via `GitHubLogin` comparison;
      no ordering asserted
- [ ] 2.2 Green: roster entry record (login, state enum
      Pending/Approved/ChangesRequested/Commented, `IsBot`, `IsMe`) and a
      named roster deriver (pure, in `Derivation`)
- [ ] 2.3 Red: `CoveredFlag` tests reworked to covering-reviewer logins --
      other human review yields its login; distinct logins for repeat
      reviewers; pending-only, own-only, and bot-only yield empty
- [ ] 2.4 Green: `CoveredFlag` returns the distinct covering logins; covered
      indicator derived from the list

## 3. QueueItem enrichment (queue-derivation)

- [ ] 3.1 Red: `QueueItem` shape tests -- sub-records (`LastUpdate`,
      `MyEngagement`) null-guarded; roster and covered-by lists guarded and
      read-only; `IsAlreadyCovered` derived from `CoveredBy`
- [ ] 3.2 Green: regroup `QueueItem` into identity, last update, state,
      has-update, roster, engagement, covered-by (constructor at 7 parameters)
- [ ] 3.3 Red: `QueueItemDeriver` tests -- last-looked passes the marker
      through (null when never looked); last-reviewed is the greatest
      submitted timestamp among the user's reviews regardless of state (null
      when none); roster and covering reviewers ride on the item
- [ ] 3.4 Green: deriver fills the new fields, reusing the existing
      latest-review-by-me mechanics; update every `QueueItem` construction
      site (RefreshQueue, tests)

## 4. Stale carry-over (polling-and-refresh)

- [ ] 4.1 Red: `OwnerStatus` test -- `LastFreshAt` exposed, null allowed
      (fresh-this-snapshot semantics)
- [ ] 4.2 Green: add `DateTimeOffset? LastFreshAt` to `OwnerStatus`
- [ ] 4.3 Red: `RefreshQueue` carry-over tests -- failed owner's items carried
      unchanged from the previous snapshot with `LastFreshAt` = previous
      snapshot instant; consecutive failures chain the original instant;
      recovery publishes fresh items with null `LastFreshAt`; never-fresh
      owner has status only, null instant, no items; removed owner leaves the
      snapshot; both the non-Ok-status path and the thrown-exception path
      carry over
- [ ] 4.4 Green: carry-over in `RefreshQueue` (read previous snapshot from the
      holder; extract a named helper so `RefreshOwnerAsync` reads as intent);
      vault-locked abort path untouched
- [ ] 4.5 Confirm the polling loop integration tests still pass end to end

## 5. Verification and closeout

- [ ] 5.1 Full build with warnings as errors, CSharpier check, all tests green
- [ ] 5.2 Coverage per convention (coverlet collector + Cobertura grep) on
      Core and GitHub test projects; no authored-code regression;
      delete `TestResults/` afterward
- [ ] 5.3 Sweep docs for contradictions with the new behavior (state doc's
      "per-owner fetch failure" wording, architecture doc if it describes
      snapshot contents); roadmap already updated
- [ ] 5.4 `openspec validate add-queue-enrichment` passes
