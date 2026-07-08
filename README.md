# pr-center

A single-user "review inbox" for GitHub pull requests. PR-Center is a self-hosted,
containerized web app that consolidates every PR awaiting your review — across
multiple orgs and your personal account — into one list, and layers a
read-vs-unread model on top of it. See [`docs/pr-center-idea.md`](docs/pr-center-idea.md)
for the full concept and [`docs/pr-center-state.md`](docs/pr-center-state.md) for the
state machines it observes.

## Developer setup

### Git hooks

This repo ships a version-controlled git hook in [`.githooks/`](.githooks):

- **`commit-msg`** — enforces [Conventional Commits](https://www.conventionalcommits.org) on the subject line.

Git does not use hooks under `.githooks/` until it is pointed at the directory.
Run once per clone:

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
