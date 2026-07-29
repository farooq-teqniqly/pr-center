## MODIFIED Requirements

### Requirement: A queue row renders each pull request's derived state
Each row SHALL render, relative to the user: an amber stripe and "Updated" badge
when the pull request has an update; a byline of who last touched it and when
(last-update author and relative time); reviewer roster chips colored by each
reviewer's state, with a distinct treatment for the user's own chip and for bot
reviewers; a covered decoration naming the covering reviewers when the pull
request is already covered; a distinct authored-by-me indicator when the pull
request's author is the user, comprising a text badge and a row shade with a
colored stripe in a hue distinct from the has-update treatment; and two
engagement/activity instants -- the user's last-reviewed instant and the
last-update instant. A pull request the user has never reviewed has no update
baseline, so it SHALL render without the update stripe and badge. A pull request
the user did not author SHALL render without the authored-by-me indicator. When a
pull request is both authored by the user and has an update, the has-update row
treatment SHALL take precedence for the shade and stripe while the authored-by-me
badge SHALL still render. State SHALL never be conveyed by color alone -- the
badge, chip, and authored-by-me indicator text carry the same meaning as text.

#### Scenario: Updated pull request shows the stripe and badge
- **WHEN** a row renders a pull request that has an update for the user
- **THEN** the amber stripe and "Updated" badge are shown

#### Scenario: Never-reviewed pull request shows no badge
- **WHEN** a row renders a pull request the user has never reviewed
- **THEN** no update stripe or badge is shown, and the row still appears in the list

#### Scenario: Roster chip marks the user and bots distinctly
- **WHEN** a row renders a roster containing the user and a bot reviewer
- **THEN** the user's chip carries the "me" treatment and the bot chip carries the bot treatment, each colored by its reviewer state

#### Scenario: Covered pull request names its coverers
- **WHEN** a row renders a pull request that is already covered
- **THEN** a covered decoration names the covering reviewers

#### Scenario: Byline is who-last-touched and when
- **WHEN** a row renders any pull request
- **THEN** the byline shows the last-update author and a relative time, with no activity-verb summary

#### Scenario: Self-authored pull request shows the authored-by-me indicator
- **WHEN** a row renders a pull request whose authored-by-me flag is set and which has no update
- **THEN** the authored-by-me badge and the authored-by-me row shade and stripe are shown, the badge carrying its meaning as text rather than by color alone

#### Scenario: Pull request authored by another shows no authored-by-me indicator
- **WHEN** a row renders a pull request whose authored-by-me flag is not set
- **THEN** no authored-by-me indicator is shown

#### Scenario: Self-authored and updated pull request keeps the has-update shade
- **WHEN** a row renders a pull request that is both authored by the user and has an update
- **THEN** the has-update shade and stripe are shown rather than the authored-by-me shade, and the authored-by-me badge is still shown
