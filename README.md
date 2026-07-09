# pr-center

A single-user "review inbox" for GitHub pull requests. PR-Center is a self-hosted,
containerized web app that consolidates every PR awaiting your review — across
multiple orgs and your personal account — into one list, and layers a
read-vs-unread model on top of it. See [`docs/pr-center-idea.md`](docs/pr-center-idea.md)
for the full concept and [`docs/pr-center-state.md`](docs/pr-center-state.md) for the
state machines it observes.

## Developer setup

### Prerequisites

- **.NET SDK 10** (`net10.0`). The exact version is pinned in [`global.json`](global.json)
  (`10.0.301`, `rollForward: latestFeature`). Verify with `dotnet --version`.

### Solution layout

- `src/PrCenter.Core` — use cases, derivers, app lock, and the ports
  (`IGitHubFacts`, `IStateStore`, `ITokenVault`). References no GitHub or EF packages.
- `src/PrCenter.GitHub` — adapter implementing `IGitHubFacts`.
- `src/PrCenter.Persistence` — EF Core + SQLite adapter (`IStateStore`, `ITokenVault`).
- `src/PrCenter.Web` — Blazor Server host and DI composition root.
- `tests/*` — sibling `<Project>.Tests` for each, plus `PrCenter.ArchitectureTests`
  (NetArchTest) enforcing the dependency direction rules.

### Build, test, format

```sh
dotnet tool restore              # restore CSharpier (local tool), once per clone
dotnet build PrCenter.slnx       # zero warnings (TreatWarningsAsErrors=true)
dotnet test PrCenter.slnx        # all test projects (xUnit v3)
dotnet csharpier format .        # apply formatting (CSharpier owns formatting)
dotnet csharpier check .         # verify formatting; run by the pre-commit hook
```

Run the app locally:

```sh
dotnet run --project src/PrCenter.Web
```

Package versions are centrally managed in [`Directory.Packages.props`](Directory.Packages.props);
`.csproj` files reference packages without a `Version` attribute.

### Git hooks

This repo ships version-controlled git hooks in [`.githooks/`](.githooks):

- **`commit-msg`** — enforces [Conventional Commits](https://www.conventionalcommits.org) on the subject line.
- **`pre-commit`** — runs `dotnet csharpier check .` and rejects commits with formatting violations.

A best-effort MSBuild target activates the hooks path on build. If it does not run,
point Git at the directory once per clone:

```sh
git config core.hooksPath .githooks
```

### Commit message format

Commit subjects must follow:

```text
<type>(<scope>)!: <description>
```

- **type** (required): `feat` `fix` `docs` `style` `refactor` `perf` `test` `build` `ci` `chore` `revert`
- **scope** (optional): lower-case component name, e.g. `(api)`
- **!** (optional): marks a breaking change
- **description** (required): short summary

Examples:

```text
feat(poll): add per-owner review-requested search
fix!: drop legacy last-seen marker format
docs: document PAT SSO caveat
```

Merge, revert, and `fixup!`/`squash!` commits are exempt.
