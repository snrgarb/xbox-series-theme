#!/usr/bin/env bash
# Posts a message, read from stdin, to a Discord channel through its webhook, $DISCORD_WEBHOOK: release.yml's posts to
# #builds. Discord is a courtesy, never a gate: with no webhook, or when Discord refuses, it warns and exits 0, so a
# release never fails over it. Mentions are off (no @everyone, roles or users), and a message over Discord's 2000
# characters is cut at its last whole line that fits.
#
#   echo "Pre-release v2026.11.8 is being built" | DISCORD_WEBHOOK=https://discord.com/api/webhooks/... build/discord.sh
set -uo pipefail

limit=2000
message="$(cat)"

if [[ -z ${DISCORD_WEBHOOK:-} ]]; then
    echo "::warning title=Discord::No webhook to post to (the DISCORD_WEBHOOK_BUILDS secret); not posted:"
    echo "$message"
    exit 0
fi

if ((${#message} > limit)); then
    cut="${message:0:limit - 2}"
    message="${cut%$'\n'*}"$'\n'"…"
fi

body="$(jq -n --arg content "$message" '{content: $content, allowed_mentions: {parse: []}}')"
if ! response="$(curl -sS --fail-with-body --retry 3 -H 'Content-Type: application/json' -d "$body" "$DISCORD_WEBHOOK" 2>&1)"; then
    echo "::warning title=Discord::Couldn't post to Discord: $response"
    echo "$message"
    exit 0
fi
echo "Posted to Discord:"
echo "$message"
