# Policy lock output

Implemented readable text summaries for check and approval: separate indented items, file-grouped suppressions with occurrence/comment, file-grouped baseline entries, inline none for empty categories. No policy diff command exists, so all items are printed (74-item regression). JSON retains its original formatter, including message text.

Tests-first commit 4edaa2341 failed both new behavioral tests before implementation (persistent red log: /home/adam/projects/scratch/axial-coord/pi/policy-lock-output-red.log). PolicyLockTests then passed 25/25. Final fail-first proof and full verification are in artifacts/verify/fail-first.txt and result.json. No concurrency changes; no policy TOML or lock edits.
