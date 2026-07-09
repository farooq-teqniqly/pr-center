# Design: add-solution-architecture

## Context

Greenfield repo. Product concept (docs/pr-center-idea.md) and state model
(docs/pr-center-state.md) are settled; the architecture was agreed in a Lucid
diagram review: layered ports-and-adapters with a pure business core, a Blazor
Server host as composition root, and adapters for GitHub and persistence. This
change lays down the solution skeleton that all feature changes build on. No
feature behavior ships here.

## Goals / Non-Goals

**Goals:**

- Project boundaries that make the architecture structurally enforceable.
- Baseline engineering scaffolding (CPM, CSharpier, hooks, warnings-as-errors)
  working end to end: `dotnet build` clean, `dotnet test` green.
- Ports defined so feature changes implement against stable seams.

**Non-Goals:**

- Any GitHub API, polling, derivation, encryption, or UI behavior.
- Containerization, migrations, OpenTelemetry (later changes).

## Decisions

### D1: Four production projects (Web / Core / GitHub / Persistence)

`PrCenter.Web` (Blazor Server host, components, polling `BackgroundService`
shell, composition root), `PrCenter.Core` (use cases, derivers, app lock,
ports), `PrCenter.GitHub` and `PrCenter.Persistence` (adapters).

- *Alternative: single project.* Rejected: dependency rules become convention
  only; Core would accrue GitHub/EF references unnoticed.
- *Alternative: full Domain/Application/Infrastructure split (5-6 projects).*
  Rejected: six use cases and three ports do not justify the ceremony; Core
  covers both domain logic and use-case orchestration.

### D2: Ports live in Core; DI binding in Web

`IGitHubFacts`, `IStateStore`, `ITokenVault` are defined in `PrCenter.Core`.
Adapters reference Core and implement its ports; `PrCenter.Web` references
everything and binds adapters to ports in DI. Core references no GitHub, EF,
or ASP.NET packages.

- *Alternative: separate contracts/abstractions project.* Rejected: only one
  consumer of the ports exists; a contracts assembly adds a project without
  adding a boundary.

### D3: Derivers are pure classes, not state machines

Membership, seen/updated, and already-covered logic land in Core as pure,
stateless classes (names like `MembershipDeriver`, `UpdateDetector` -- not
`*StateMachine`). Verified against docs/pr-center-state.md: membership is
recomputed each poll as a pure function of GitHub facts with no stored
transition history. Naming them state machines invites someone to add stored
transitions later. The only true runtime state machine is the app lock
(Locked/Unlocked), a Core singleton service that gates polling and the token
vault. This change creates the types as compilable stubs only; behavior comes
with its own specs and tests in later changes.

### D4: Architecture tests with NetArchTest.eNhancedEdition

Dedicated `PrCenter.ArchitectureTests` project (xUnit v3) asserting, at
minimum: Core does not depend on GitHub/EF/ASP.NET assemblies or on adapter
projects; adapters do not depend on each other; only Web depends on adapters.
Violations fail `dotnet test`, so the boundary rules are executable.

- *Package choice:* [NetArchTest.eNhancedEdition](https://github.com/NeVeSpl/NetArchTest.eNhancedEdition)
  over the original NetArchTest (user decision): actively maintained fork,
  same fluent API.
- *Placement:* a solution-wide concern does not belong to any one
  `<Project>.Tests` sibling; a dedicated project is a deliberate addition to
  the baseline's one-test-project-per-production-project layout.

### D5: Persistence skeleton is an empty EF Core DbContext

`PrCenter.Persistence` gets `PrCenterDbContext` with no entities plus stub
`IStateStore`/`ITokenVault` implementations, enough to prove DI wiring and the
SQLite package reference compile. Schema, migrations, and crypto (KDF + AES)
arrive with the changes that specify their behavior.

### D6: Tooling per baseline

net10.0 (`global.json`, rollForward latestFeature), Central Package
Management, `Directory.Build.props` with `TreatWarningsAsErrors=true` and
SonarAnalyzer, CSharpier local tool + pre-commit hook, `nuget.config` single
source with package source mapping, LF/CRLF rules in `.gitattributes` +
`.editorconfig`. All copied behavior from the baseline doc, not re-decided
here.

### D7: No bundled Bootstrap -- CDN with pinned versions and SRI

The Blazor template ships an outdated Bootstrap copy under `wwwroot`. Delete
it. `App.razor` loads Bootstrap CSS/JS from the jsdelivr CDN with an exact
pinned version and `integrity`/`crossorigin` (SRI) attributes, and fonts from
Google Fonts with `preconnect` hints -- following the pattern in
[trakmark's App.razor](https://github.com/farooq-teqniqly/trakmark/blob/main/Trakmark/Components/App.razor).
Only app-specific CSS stays in `wwwroot`.

- *Trade-off:* the app is self-hosted and normally used with network access
  (it polls GitHub anyway), so CDN reliance costs nothing in practice; if the
  CDN is unreachable the styling degrades but GitHub polling is equally down.
- *Rule:* any CDN asset must pin an exact version and carry an SRI integrity
  hash -- no `@latest` references.

## Risks / Trade-offs

- [Stubs ship without behavior] -> stub members throw `NotImplementedException`
  and carry no fake logic, so accidental use fails loudly; no tests assert on
  stub output (baseline forbids trivially-true tests).
- [Arch tests couple to assembly/namespace names] -> keep rules on project
  (assembly) references, not namespace string matching, where the API allows.
- [net10.0 SDK availability on CI/workstation] -> `global.json` pins the SDK;
  README documents the required version. No CI exists yet, so the risk is
  local-only for now.
- [Blazor template noise (sample pages, weather demo)] -> strip template
  content to a bare shell in this change so later UI diffs stay readable.

## Migration Plan

Greenfield; nothing to migrate. Rollback = delete the branch.

## Open Questions

- REST vs GraphQL mix inside `PrCenter.GitHub` -- deferred to the GitHub
  adapter change; the `IGitHubFacts` port is transport-neutral either way.
- KDF library choice (Argon2 vs PBKDF2 implementation) -- deferred to the
  security/token-vault change.
