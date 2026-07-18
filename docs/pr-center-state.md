# PR-Center State Model

PR-Center is a read-only projection of GitHub state — it never mutates a PR. This doc makes the state machines it observes and derives explicit, so the list logic ("does this PR show, is it flagged updated, is it already covered") has one authoritative reference. It complements [pr-center-idea.md](./pr-center-idea.md); where they disagree, the Key Decisions in that doc win and this doc should be corrected.

There are three machines:

1. **Membership** — whether a PR appears in my list at all, and why.
2. **Updated** — for a shown PR, whether another person has acted on it since I last reviewed it. A pure derivation, no stored seen state. Orthogonal to membership.
3. **App lock** — whether the app is unlocked (tokens decrypted, polling active).

---

## 1. Membership lifecycle

Whether a PR is in my list. All transitions are evaluated **relative to me** — another reviewer's actions never move a PR between these states.

```mermaid
stateDiagram-v2
    [*] --> NotShown

    state NotShown {
        [*] --> Untracked
        Untracked: Untracked (not requested, never reviewed by me)
        Draft: Excluded (draft PR)
        Approved: Dropped (I approved)
        Closed: Dropped (PR closed / merged)
    }

    NotShown --> AwaitingFirstReview: I am added as a requested reviewer (and PR is not a draft)
    NotShown --> Draft: PR is a draft while I am requested

    state Shown {
        AwaitingFirstReview: Awaiting my first review (requested, no review by me yet)
        AwaitingReReview: Awaiting my re-review (I reviewed: commented or changes-requested)
        AwaitingFirstReview --> AwaitingReReview: I submit a non-approved review (commented / changes-requested)
        AwaitingReReview --> AwaitingReReview: I submit another non-approved review
    }

    Draft --> AwaitingFirstReview: PR marked ready for review (I am still requested)
    AwaitingFirstReview --> Draft: author converts PR to draft
    AwaitingReReview --> Draft: author converts PR to draft

    AwaitingFirstReview --> Approved: I submit an approving review
    AwaitingReReview --> Approved: I submit an approving review

    AwaitingFirstReview --> Closed: PR closed / merged
    AwaitingReReview --> Closed: PR closed / merged

    Approved --> AwaitingFirstReview: author re-requests my review
```

Notes:
- **Shown = { AwaitingFirstReview, AwaitingReReview }.** These are the only states that appear in the list.
- **Membership is derived, not remembered.** Each poll recomputes a PR's state as a pure function of current GitHub facts (am I a requested reviewer? do I have a prior non-approved review on this PR? is it a draft/closed?). There is no stored transition history. This is why the diagram's arrows are illustrative: the app never "remembers" it was in a state — it just recomputes. Consequently a draft marked ready lands in `AwaitingReReview` iff I already have a non-approved review on it, else `AwaitingFirstReview`, with no special-casing.
- **Draft** is a hidden state, not a removal — the PR reappears (with its state recomputed as above) when marked ready.
- **Approved** and **Closed** are terminal for the current review round. Approved can be re-entered into the list only by an author re-request. Closed is terminal unless GitHub reopens the PR.
- **No app-side staleness expiry:** a PR in `AwaitingReReview` stays there indefinitely while open; only my approval or a GitHub close removes it.

---

## 2. Updated (per shown PR)

Orthogonal to membership: for any PR currently in `Shown`, has another person acted on it since I last reviewed it? This is the "update indicator." It is a pure derivation each poll -- there is no stored per-PR "seen" state (revised 2026-07-17).

```mermaid
stateDiagram-v2
    [*] --> New: PR first enters my list (I have not reviewed it)

    New: New (no review by me yet -- no baseline, no badge)
    UpToDate: Up to date (no other activity after my last review)
    Updated: Updated (other activity after my last review)

    New --> UpToDate: I submit a review on GitHub (baseline now exists)
    UpToDate --> Updated: poll detects OTHER people's activity after my last review (new commit / comment / review)
    Updated --> UpToDate: I review again on GitHub --> my review advances the baseline past that activity
    UpToDate --> UpToDate: only my own activity, or a bare updatedAt bump (labels/title/base) --> no change
```

Notes:
- The baseline is **my own latest review instant**, a GitHub fact recomputed every poll -- not a stored marker. There is no "seen" state to persist and nothing to clean up or GC.
- A PR I have **never reviewed** has no baseline, so it is **New**, not Updated -- it shows **without** an update badge. The badge is meaningful only once I have a review to measure against.
- "Update" = another person's new commit/push, new comment/reply, or new review with a timestamp strictly after my last review. **My own activity is the baseline, not an update**, and bare `updatedAt` bumps do not count. **Bot/CI comments and reviews do not count either** (a qodo or Copilot comment is noise, not a reason to re-look); **bot commits DO count** — a new commit is a real diff to review regardless of who authored it. Bot = the actor's type per the GitHub API (`user.type`/`__typename` == `Bot`), never login-text sniffing (see the 2026-07-10 GitHub adapter spike).
- **The indicator clears when I review on GitHub**, not when I click the in-app link. Clicking the link only opens the PR; opening and closing it without reviewing changes nothing, so a real update can never be silently cleared. The next poll (auto or manual) recomputes the state.

### "Already covered" — a derived flag, not a state
Independent of the update indicator: a PR is flagged **already covered** when ≥1 *other* **human** reviewer has submitted any non-dismissed review (approved / changes-requested / commented). Pending (requested, no review) does not count, **and bot/CI reviews (qodo, Copilot, etc.) do not count** — a bot review is not human coverage. This is a display decoration that signals low marginal value; it never hides or moves the PR. (Decided 2026-07-10, resolving the former open question here; verified against real payloads in the GitHub adapter spike — e.g. a PR whose only human review was dismissed correctly derives as not covered.)

---

## 3. App lock

Gates decryption of the stored tokens and therefore all GitHub access, including background polling.

```mermaid
stateDiagram-v2
    [*] --> Uninitialized: container start, no app password set
    [*] --> Locked: container start, app password already set
    Uninitialized --> Locked: set app password (write salt + Argon2id params + encrypted sentinel)
    Locked --> Unlocked: correct app password --> Argon2id re-derives key --> sentinel decrypts (tag verifies) --> tokens decrypted in memory
    Unlocked --> Locked: container stops
    Locked --> Locked: wrong password (sentinel tag fails)
    Unlocked --> Uninitialized: reset (wipe tokens + app-security row)
    Locked --> Uninitialized: reset (wipe tokens + app-security row)
```

Notes:
- **Three states.** `Uninitialized` (no app password set yet), `Locked` (password set, key not in memory), `Unlocked` (key held). State is derived, not stored: a security row exists iff a password was set, and a key is held iff unlocked. The distinction matters for onboarding -- an uninitialized app shows "set a password," a locked one shows "enter your password."
- **Setting the password does not unlock.** First-run set-password writes the salt, Argon2id parameters, and encrypted sentinel, leaving the app `Locked`; the user then unlocks with the same password. Password verification uses the sentinel: a failed AES-GCM authentication tag means a wrong password, and it works even before any token is stored.
- **No polling or data while Locked** — the key is required to call GitHub. After a restart, the list is empty until I unlock.
- **No auto-lock / idle timeout in v1** — once unlocked, stays unlocked until the container stops. The decrypted key lives server-side in the Blazor process, shared across browser tabs.
- **Forgotten password has no recovery** — reset wipes stored tokens and the security row, returning the app to `Uninitialized`; I set a new password and re-enter all three PATs.
