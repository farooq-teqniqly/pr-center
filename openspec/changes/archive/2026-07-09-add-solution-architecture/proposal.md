# Proposal: add-solution-architecture

## Why

PR-Center has agreed product docs (idea, state model) and an agreed architecture
diagram, but no code. Every feature change that follows needs a solution skeleton
whose project boundaries enforce the architecture: a pure business core behind
ports, thin adapters for GitHub and persistence, and a Blazor Server host as the
composition root. Establishing that skeleton first keeps later changes small and
prevents the core from quietly growing GitHub or EF dependencies.

## What Changes

- Create the .NET solution with four production projects and their sibling test
  projects:
  - `PrCenter.Web` - Blazor Server host, UI components, polling
    `BackgroundService`, DI composition root.
  - `PrCenter.Core` - use cases, membership/update derivers, already-covered
    flag, app lock state machine, and the ports `IGitHubFacts`, `IStateStore`,
    `ITokenVault`. References no GitHub or EF packages.
  - `PrCenter.GitHub` - adapter implementing `IGitHubFacts` against the GitHub
    REST/GraphQL API, one PAT per owner.
  - `PrCenter.Persistence` - adapter implementing `IStateStore` and
    `ITokenVault` with EF Core + SQLite and the token vault crypto (KDF + AES).
- Establish repo-wide engineering scaffolding per the baseline:
  `Directory.Build.props`, `Directory.Packages.props` (Central Package
  Management), `global.json` (net10.0 SDK, rollForward latestFeature),
  `nuget.config` with package source mapping, `.editorconfig`/`.gitattributes`
  line-ending rules, CSharpier as a local dotnet tool, `.githooks` activation.
- Define port interfaces and the DI wiring shape (adapters bound to Core ports
  in `PrCenter.Web`); implementations are stubs that compile and are covered by
  placeholder-free smoke tests only where behavior exists.
- Enforce the dependency direction rules with architecture tests
  ([NetArchTest.eNhancedEdition](https://github.com/NeVeSpl/NetArchTest.eNhancedEdition),
  the maintained fork of NetArchTest) in a dedicated
  `PrCenter.ArchitectureTests` project: Core references no GitHub/EF/adapter
  assemblies, adapters do not reference each other, only Web references
  adapters. Rules run as ordinary xUnit tests so violations fail the build.
- Solution builds clean with `TreatWarningsAsErrors=true` and all test projects
  run under xUnit v3.

## Non-goals

- No feature behavior: no GitHub API calls, no polling loop logic, no
  membership/update derivation logic, no encryption implementation, no UI
  beyond the Blazor template shell. Those arrive as separate spec-driven
  changes.
- No Dockerfile/Podman compose yet - containerization is its own change.
- No database schema/migrations beyond an empty `DbContext` placeholder if
  needed for wiring.
- No OpenTelemetry wiring yet.

## Capabilities

### New Capabilities

- `solution-structure`: the solution's project layout, dependency direction
  rules (Core references nothing app-specific; adapters reference Core; Web
  references all), port definitions, and the build/tooling conventions
  (CPM, CSharpier, hooks, warnings-as-errors) that every later change builds
  on.

### Modified Capabilities

*None - first change in the repo; no existing specs.*

## Impact

- New solution/projects at repo root; no existing code affected (greenfield).
- `README.md` gains build/test instructions.
- Later changes (GitHub adapter behavior, derivers, polling, UI, security,
  containerization) all depend on this skeleton and its dependency rules.
