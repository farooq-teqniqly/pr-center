# Tasks: add-solution-architecture

## 1. Repo scaffolding

- [x] 1.1 Add `global.json` (net10.0 SDK, rollForward latestFeature) and verify `dotnet --version` resolves
- [x] 1.2 Add `nuget.config` (single nuget.org source with package source mapping)
- [x] 1.3 Add `Directory.Build.props` (Nullable, ImplicitUsings, TreatWarningsAsErrors, CodeAnalysisTreatWarningsAsErrors=false, SonarAnalyzer, test package auto-injection for `*Tests` projects)
- [x] 1.4 Add `Directory.Packages.props` with CPM enabled (empty version list to start)
- [x] 1.5 Add `.editorconfig` and `.gitattributes` line-ending rules (LF except .sln/.ps1/.bat/.cmd) and keep the two aligned
- [x] 1.6 Add `dotnet-tools.json` with CSharpier local tool; run `dotnet tool restore`
- [x] 1.7 Add pre-commit hook in `.githooks/` running CSharpier check alongside the existing commit-msg hook; verify `core.hooksPath` activation (best-effort build target + fallback documented)

## 2. Projects and solution

- [x] 2.1 Create solution file and `PrCenter.Core` project (net10.0, classlib)
- [x] 2.2 Create `PrCenter.GitHub` and `PrCenter.Persistence` projects, each referencing `PrCenter.Core` only
- [x] 2.3 Create `PrCenter.Web` Blazor Server project referencing Core + both adapters
- [x] 2.4 Create sibling test projects (`PrCenter.Core.Tests`, `PrCenter.GitHub.Tests`, `PrCenter.Persistence.Tests`, `PrCenter.Web.Tests`) on xUnit v3 + NSubstitute (bUnit in Web.Tests)
- [x] 2.5 Register all projects in the solution; `dotnet build` clean, `dotnet test` green (empty suites)

## 3. Ports and stubs

- [x] 3.1 Define `IGitHubFacts`, `IStateStore`, `ITokenVault` in `PrCenter.Core` with XML docs (minimal member sets -- only what the skeleton needs; feature changes extend them)
- [x] 3.2 Add stub `GitHubFactsClient` in `PrCenter.GitHub` implementing `IGitHubFacts`; unimplemented members throw `NotImplementedException`
- [x] 3.3 Add empty `PrCenterDbContext` (EF Core + SQLite packages) and stub `IStateStore`/`ITokenVault` implementations in `PrCenter.Persistence`; unimplemented members throw `NotImplementedException`
- [x] 3.4 Write failing DI resolution test in `PrCenter.Web.Tests` (host builds; resolving each port yields the adapter type), then add the DI registrations in `PrCenter.Web` to make it pass

## 4. Architecture tests

- [x] 4.1 Create `PrCenter.ArchitectureTests` project with NetArchTest.eNhancedEdition (version in `Directory.Packages.props`)
- [x] 4.2 Write rule: `PrCenter.Core` has no dependency on adapters, Web, EF Core, or ASP.NET Core assemblies
- [x] 4.3 Write rule: adapters do not depend on each other; only Web depends on adapters
- [x] 4.4 Prove the rules bite: temporarily add a violating reference locally, confirm a test fails, revert

## 5. Web shell cleanup

- [x] 5.1 Strip Blazor template sample content (weather/counter pages, bundled Bootstrap under `wwwroot`)
- [x] 5.2 Rework `App.razor`: Bootstrap CSS/JS from jsdelivr CDN pinned with SRI `integrity`/`crossorigin`, fonts from font CDN with preconnect; keep only app-specific CSS in `wwwroot`
- [x] 5.3 Verify the shell renders (app starts, page loads with CDN styles)

## 6. Verification and docs

- [x] 6.1 Full pass: `dotnet build` (zero warnings), `dotnet test` (all projects), `dotnet csharpier check .` all clean
- [x] 6.2 Verify no `.csproj` carries a `PackageReference` `Version` attribute (CPM scenario)
- [x] 6.3 Update `README.md` with build/test/format instructions and required SDK
