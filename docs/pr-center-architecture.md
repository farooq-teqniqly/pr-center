# PR-Center Architecture

The canonical architecture picture is a Lucid diagram:
<https://lucid.app/lucidchart/bc093d17-51ce-4975-a398-caebcb69d817>
(open with your Lucid account; the source of truth for project boundaries and
dependency direction). This doc is the checked-in prose mirror of that diagram
so changes and reviewers -- and tools without Lucid access -- can rely on it
without re-deriving the layout. Where this doc and the diagram disagree, update
both in the same pass; where either disagrees with
[pr-center-idea.md](./pr-center-idea.md) or [pr-center-state.md](./pr-center-state.md),
those win.

Ports-and-adapters (hexagonal) with a pure business core. Four production
projects; the dependency rule is enforced by `PrCenter.ArchitectureTests`
(see the archived `add-solution-architecture` change).

## Layers and dependency direction

```text
  PrCenter.Web  (Presentation + Host / composition root)
        │  depends on everything; binds adapters to ports in DI
        ▼
  PrCenter.Core (Business layer -- pure, NO GitHub/EF/ASP.NET references)
        ▲                                   ▲
        │ implements                        │ implements
  PrCenter.GitHub (adapter)          PrCenter.Persistence (adapter)
        │ HTTPS                             │
        ▼                                   ▼
    GitHub API                    SQLite file on mounted volume
```

Dependency arrows point **inward** to Core. Core defines the ports; adapters
reference Core and implement them; only `PrCenter.Web` references the adapters
and binds them in DI. **Core references no GitHub, EF, or ASP.NET packages.**
Adapters never reference each other.

## PrCenter.Web -- Presentation + Host (composition root)

- **Blazor Server components** -- queue list, settings, unlock.
- **Polling BackgroundService** -- an interval timer and every on-demand
  refresh (manual refresh, unlock) poke a single refresh trigger; the loop
  awaits that one trigger and, on each wake while the app is Unlocked, drives
  the `RefreshQueue` use case in Core.

## PrCenter.Core -- Business layer (pure, no I/O)

Use cases (application services):

- **RefreshQueue** -- orchestrates a poll: enumerate owners, fetch facts per
  owner, run the derivers against the stored last-seen markers, and publish an
  in-memory queue snapshot with per-owner fetch status. (I/O orchestration;
  lands in the polling-and-refresh change, not the derivation change.)
- **GetQueue** -- returns the current queue snapshot for display, or an explicit
  never-polled result.
- **MarkSeen (live fetch)** -- on click-through, fresh live fetch of that PR
  before writing the last-seen marker.
- **UnlockApp** -- app-password unlock via the app lock; on success pokes the
  refresh trigger for an immediate first poll.
- **SaveOwnerToken / Settings** -- PAT entry and settings persistence.
- **ResetVault (wipe tokens)** -- no-recovery wipe path.

Derivers (pure functions, recomputed each poll -- **not** stored state
machines; see [pr-center-state.md](./pr-center-state.md)):

- **Membership deriver** -- pure fn per poll; decides whether a PR is shown and
  in which shown state, relative to the user.
- **Update detector** -- signals a PR has an update by comparing current facts
  against the last-seen marker; other people's activity only.
- **Already-covered flag** -- derived display decoration; true when >=1 other
  reviewer has submitted any review.

Runtime state:

- **App Lock state machine** -- the only true runtime FSM; gates polling and
  the token vault. Three states: `Uninitialized` (no app password set),
  `Locked` (password set, key not in memory), `Unlocked` (key held). Setting
  the password moves `Uninitialized -> Locked`; unlocking moves
  `Locked -> Unlocked`; reset returns to `Uninitialized`.

Ports (defined in Core, implemented by adapters):

- **IGitHubFacts** -- reads raw PR facts for an owner. Its return type (the
  PR facts model) is Core-owned contract surface: the GitHub adapter produces
  it, the derivers and `RefreshQueue` consume it.
- **IStateStore** -- persists per-PR last-seen markers (and later owner
  settings).
- **ITokenVault** -- encrypted token storage at rest (set password, store/get
  per-owner tokens, reset).
- **IAppLock** -- the lock-state gate: derives `Uninitialized`/`Locked`/`Unlocked`
  and performs unlock, holding the decrypted key in a process-wide singleton.

## PrCenter.GitHub -- adapter

REST/GraphQL client, one fine-grained PAT per owner, rate-limit handling, and
per-owner fetch status (ok / error / needs-SSO). Talks to the GitHub API over
HTTPS. Implements `IGitHubFacts`.

## PrCenter.Persistence -- adapter

EF Core + SQLite for markers, tokens, and settings; token-vault crypto
(Argon2id KDF + AES-GCM at rest). Writes to a SQLite file on a mounted host
volume. Implements `IStateStore`, `ITokenVault`, and `IAppLock` (the latter
backed by a process-wide singleton key holder).

## Crosscutting

OpenTelemetry (traces, metrics, logs) wired in the host, spanning all layers.
Deferred to the observability change.
