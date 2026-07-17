# Copilot review instructions for PR-Center

PR-Center is a single-user, self-hosted "review inbox" for GitHub PRs, in
C#/.NET (net10.0) with a ports-and-adapters layout: a pure `PrCenter.Core`
behind ports, `PrCenter.GitHub` / `PrCenter.Persistence` adapters, and a
`PrCenter.Web` Blazor Server host. Full conventions live in `CLAUDE.md` and
`CLAUDE-baseline.md`.

When reviewing, please respect these deliberate project decisions and **do not
flag them as defects**:

- **Facts records omit runtime null-element guards on their non-nullable
  collections.** `PrCenter.Core.Facts` types (e.g. `PullRequestActivity`) and
  `OwnerFactsResult` copy their collections into read-only wrappers and guard
  the list references and blank *login strings*, but intentionally do **not**
  guard for null *elements* of non-nullable record collections
  (`IReadOnlyList<ReviewFact>` etc.). The element types are non-nullable, the
  only producer never inserts null, and this was decided in issue #10 (Option
  A) after review. Do not recommend restoring element-null guards on these.
- **DI-injected dependencies are not null-guarded.** Any constructor parameter
  supplied by the DI container -- including `HttpClient`, `ITokenVault`,
  `ILogger<T>`, and `PrCenterDbContext` -- is trusted and deliberately has no
  `ArgumentNullException` check (e.g. `StateStore(PrCenterDbContext, ILogger<StateStore>)`).
  Only non-DI public/internal entry points guard their reference-type parameters.
- **EF Core-generated migration classes are `public partial` by design.** Files
  under `Migrations/` (e.g. `*_InitialCreate.cs`, the model snapshot) are emitted
  by the EF tooling; their `public` accessibility is the generator's, and
  regenerating reverts any hand-narrowing. Do not flag their visibility or
  suggest making them `internal`/`sealed`.
- **Source-generated logging is implemented by the generator.** `*.Logging.cs`
  files hold `partial void Log...` methods annotated with `[LoggerMessage]`;
  the source generator supplies the bodies. They are not unimplemented partial
  methods.
- **EF Core write/upsert paths track deliberately.** Read-only queries use
  `AsNoTracking()` by convention, but methods that load an entity to modify and
  save it (e.g. `StateStore.SetLastSeenAsync` using `FindAsync`) require change
  tracking. Do not flag those as missing `AsNoTracking`.
- **`array.AsReadOnly()` and `list.AsReadOnly()` are valid and compile.**
  `System.Collections.Generic.CollectionExtensions.AsReadOnly<T>(this IList<T>)`
  ships in the BCL (.NET 7+); since `T[]` implements `IList<T>`, an idiom like
  `source.ToArray().AsReadOnly()` binds to that extension and returns a
  `ReadOnlyCollection<T>`. It requires no project-defined extension method. Do
  not claim these calls do not compile or that they need `Array.AsReadOnly(...)`
  instead -- both forms are correct; the instance-style call is a deliberate
  choice (target framework is net10.0).

Contracts worth knowing (flag genuine violations, not the pattern itself):

- **`IGitHubFacts` fetch failures are returned, not thrown.**
  `GetReviewQueueFactsAsync` returns an `OwnerFetchStatus` (Ok /
  MisconfiguredToken / Error) and `GetPullRequestFactsAsync` returns null for
  any fetch failure -- including timeouts, which surface as
  `OperationCanceledException` with the caller's token *not* cancelled. A
  genuine caller cancellation (token requested) is the only exception that
  propagates.
- Access tokens must never appear in logs or in an `OwnerFactsResult` detail.
- Default accessibility is `internal sealed`; tests are xUnit v3 + NSubstitute;
  formatting is owned by CSharpier.
