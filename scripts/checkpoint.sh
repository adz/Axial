#!/usr/bin/env bash

# Commits the working tree as a local WIP checkpoint before an agent run, so the run can be undone with one command
# (`git reset --hard <printed sha>`). Checkpoints are never pushed: squash them into the task's real commit with
# `git reset --soft <base>` followed by one `git commit`, where <base> is the last pushed commit.
#
#   scripts/checkpoint.sh "before T5 review fixes"

set -euo pipefail

label="${1:?usage: checkpoint.sh <label>}"
cd "$(git rev-parse --show-toplevel)"

if [[ -n $(git status --porcelain) ]]; then
    git add -A
    git commit -q --no-verify -m "WIP checkpoint: $label"
fi

git rev-parse --short HEAD
