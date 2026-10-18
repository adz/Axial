#!/usr/bin/env bash

# Commits the working tree as a local WIP checkpoint before an agent run and prints `restore=<sha>`: the commit to
# return to with `git reset --hard <sha>` if the run goes wrong. With a clean tree no checkpoint commit is made and
# <sha> is HEAD. Checkpoints are never pushed: squash with `git reset --soft <last pushed commit>` and one
# `git commit`; never reset to "the checkpoint's parent", which goes one commit too far when nothing was committed.
#
#   scripts/checkpoint.sh "before T5 review fixes"

set -euo pipefail

label="${1:?usage: checkpoint.sh <label>}"
cd "$(git rev-parse --show-toplevel)"

if [[ -n $(git status --porcelain) ]]; then
    git add -A
    git commit -q --no-verify -m "WIP checkpoint: $label"
fi

echo "restore=$(git rev-parse --short HEAD)"
