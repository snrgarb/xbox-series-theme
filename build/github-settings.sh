#!/usr/bin/env bash
# Puts this repository's GitHub settings in the state docs/development.md describes: PRs land by squash only, titled and
# described from the PR, their branch deleted after; and every ruleset in .github/rulesets/ is created, or updated in
# place when one of that name exists. Run it again after changing a ruleset's JSON. Needs `gh` signed in as an admin, and `jq`.
#   build/github-settings.sh            apply
#   build/github-settings.sh --dry-run  show what would change, change nothing
set -euo pipefail

dry=0
[[ ${1:-} == --dry-run ]] && dry=1
repo="$(gh repo view --json nameWithOwner --jq .nameWithOwner)"
root="$(git rev-parse --show-toplevel)"

run() { if ((dry)); then echo "would run: $*"; else "$@"; fi; }

echo "Merge settings for $repo"
run gh api --method PATCH "repos/$repo" --silent \
    -F allow_squash_merge=true -F allow_merge_commit=false -F allow_rebase_merge=false \
    -f squash_merge_commit_title=PR_TITLE -f squash_merge_commit_message=PR_BODY \
    -F delete_branch_on_merge=true -F allow_auto_merge=true -F allow_update_branch=true

for file in "$root"/.github/rulesets/*.json; do
    name="$(jq -r .name "$file")"
    id="$(gh api "repos/$repo/rulesets" --jq ".[] | select(.name == \"$name\") | .id")"
    if [[ -n $id ]]; then
        echo "Ruleset '$name': updating #$id from ${file#"$root"/}"
        run gh api --method PUT "repos/$repo/rulesets/$id" --input "$file" --silent
    else
        echo "Ruleset '$name': creating from ${file#"$root"/}"
        run gh api --method POST "repos/$repo/rulesets" --input "$file" --silent
    fi
done

echo "This clone: git hooks from .githooks/ (refuses commits on main)"
run git -C "$root" config core.hooksPath .githooks
