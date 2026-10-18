#!/usr/bin/env bash
# Mirror packages/sdk-csharp into https://github.com/RikuTheFuffs/curated-sdk
# and push main. Requires GitHub credentials with write access to curated-sdk.
set -euo pipefail

SDK_DIR="$(cd "$(dirname "$0")/.." && pwd)"
REMOTE_URL="${CURATED_SDK_REMOTE:-https://github.com/RikuTheFuffs/curated-sdk.git}"
WORK="${CURATED_SDK_WORKTREE:-/tmp/curated-sdk-publish}"
BRANCH="${CURATED_SDK_BRANCH:-main}"

rm -rf "$WORK"
mkdir -p "$WORK"

if git ls-remote "$REMOTE_URL" HEAD &>/dev/null; then
  git clone --depth 1 "$REMOTE_URL" "$WORK"
  find "$WORK" -mindepth 1 -maxdepth 1 ! -name '.git' -exec rm -rf {} +
else
  git init -b "$BRANCH" "$WORK"
  git -C "$WORK" remote add origin "$REMOTE_URL"
fi

# Copy SDK tree (exclude build outputs and nested .git).
rsync -a --delete \
  --exclude '.git/' \
  --exclude '**/bin/' \
  --exclude '**/obj/' \
  --exclude 'artifacts/' \
  --exclude '**/TestResults/' \
  "$SDK_DIR/" "$WORK/"

# curated-sdk repo root layout: promote nested workflow to root .github
mkdir -p "$WORK/.github/workflows"
if [[ -f "$WORK/.github/workflows/release.yml" ]]; then
  :
elif [[ -f "$SDK_DIR/.github/workflows/release.yml" ]]; then
  cp -f "$SDK_DIR/.github/workflows/release.yml" "$WORK/.github/workflows/release.yml"
fi

# Drop monorepo-only sync helper notes that assume packages/ path if undesired — keep README.

git -C "$WORK" add -A
if git -C "$WORK" diff --cached --quiet; then
  echo "No changes to push to curated-sdk."
  exit 0
fi

git -C "$WORK" -c user.name="Curated Bot" -c user.email="bot@curated.games" \
  commit -m "Sync Curated.Sdk from monorepo packages/sdk-csharp"

git -C "$WORK" push -u origin "HEAD:${BRANCH}"
echo "Pushed to $REMOTE_URL (${BRANCH})"
