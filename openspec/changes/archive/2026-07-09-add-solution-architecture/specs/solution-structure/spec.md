# solution-structure

## ADDED Requirements

### Requirement: Solution project layout

The solution SHALL contain exactly four production projects -- `PrCenter.Web`
(Blazor Server host and composition root), `PrCenter.Core` (use cases,
derivers, app lock, ports), `PrCenter.GitHub` (GitHub adapter), and
`PrCenter.Persistence` (EF Core + SQLite adapter) -- plus a sibling
`<Project>.Tests` project for each and a solution-wide
`PrCenter.ArchitectureTests` project. Every project SHALL be registered in the
solution file.

#### Scenario: Solution builds

- **WHEN** `dotnet build` runs at the repo root
- **THEN** all production and test projects compile with zero warnings
  (`TreatWarningsAsErrors=true`)

#### Scenario: All tests run

- **WHEN** `dotnet test` runs at the repo root
- **THEN** every test project executes under xUnit v3 and the run succeeds

### Requirement: Dependency direction is enforced by architecture tests

`PrCenter.Core` SHALL NOT reference GitHub API, EF Core, or ASP.NET Core
packages, nor any adapter project. Adapter projects SHALL reference only
`PrCenter.Core` (plus their own infrastructure packages) and SHALL NOT
reference each other. Only `PrCenter.Web` SHALL reference the adapter
projects. These rules SHALL be asserted by NetArchTest.eNhancedEdition rules
in `PrCenter.ArchitectureTests` so that a violation fails `dotnet test`.

#### Scenario: Core stays pure

- **WHEN** the architecture tests run
- **THEN** a rule asserts `PrCenter.Core` has no dependency on
  `PrCenter.GitHub`, `PrCenter.Persistence`, `PrCenter.Web`, EF Core,
  or ASP.NET Core assemblies

#### Scenario: Adapters are isolated from each other

- **WHEN** the architecture tests run
- **THEN** a rule asserts `PrCenter.GitHub` does not depend on
  `PrCenter.Persistence` and vice versa

#### Scenario: Violation breaks the build

- **WHEN** a project reference is added that violates a dependency rule
- **THEN** at least one architecture test fails

### Requirement: Ports are defined in Core and bound in Web

`PrCenter.Core` SHALL define the port interfaces `IGitHubFacts`,
`IStateStore`, and `ITokenVault`. Each adapter SHALL implement its port(s):
`PrCenter.GitHub` implements `IGitHubFacts`; `PrCenter.Persistence` implements
`IStateStore` and `ITokenVault`. `PrCenter.Web` SHALL register the adapter
implementations against the Core port interfaces in its DI composition root.

#### Scenario: Host resolves all ports

- **WHEN** the `PrCenter.Web` host is built (test server or app startup)
- **THEN** resolving `IGitHubFacts`, `IStateStore`, and `ITokenVault` from DI
  succeeds and yields the adapter implementations

#### Scenario: Stubs fail loudly

- **WHEN** a not-yet-implemented port member on a stub adapter is invoked
- **THEN** it throws `NotImplementedException` rather than returning fake data

### Requirement: Central package management

All NuGet package versions SHALL be declared in `Directory.Packages.props`;
`.csproj` files SHALL reference packages without version attributes.
`nuget.config` SHALL define a single `nuget.org` source with package source
mapping.

#### Scenario: No versions in project files

- **WHEN** any `.csproj` in the solution is inspected
- **THEN** its `PackageReference` items carry no `Version` attribute

### Requirement: Formatting and hooks scaffolding

CSharpier SHALL be installed as a local dotnet tool (`dotnet-tools.json`) and
own all formatting. Version-controlled git hooks in `.githooks/` SHALL be
activated via `core.hooksPath`, including the existing `commit-msg` hook and a
pre-commit hook that runs the CSharpier check.

#### Scenario: Formatting check passes on a clean tree

- **WHEN** `dotnet csharpier check .` runs at the repo root
- **THEN** it reports no formatting violations

### Requirement: Web static assets come from CDN, not the template bundle

`PrCenter.Web` SHALL NOT ship the Blazor template's bundled Bootstrap files
under `wwwroot`. `App.razor` SHALL load Bootstrap CSS/JS from a CDN pinned to
an exact version with `integrity` (SRI) and `crossorigin` attributes, and any
web fonts from a font CDN. Only app-specific CSS SHALL live in `wwwroot`.

#### Scenario: No bundled Bootstrap

- **WHEN** the `PrCenter.Web/wwwroot` directory is inspected
- **THEN** it contains no Bootstrap CSS/JS files

#### Scenario: CDN references are pinned and integrity-checked

- **WHEN** `App.razor` is inspected
- **THEN** every CDN `<link>`/`<script>` for Bootstrap pins an exact version
  and carries `integrity` and `crossorigin` attributes, with no `latest`
  version references
