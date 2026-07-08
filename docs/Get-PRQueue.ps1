# Before running this, ensure you have the Github CLI installed
# Then ensure you are logged in.
# `gh auth status` will tell you if you are logged in.
# If you are not, run `gh auth login`.

$me = "farooq-teqniqly"
$owner = "PerfectServe"

$prs = gh search prs `
  --review-requested $me `
  --owner $owner `
  --state open `
  --json number,title,repository,url,author,createdAt,updatedAt `
  --limit 50 `
| ConvertFrom-Json

$filtered = $prs | Where-Object {
    -not ($_.reviews.state -contains "APPROVED")
}

$filtered