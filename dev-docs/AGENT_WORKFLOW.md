# Agent workflow

How a coordinating agent works the `TASKS.md` queue with delegated agents, keeps its own context small, and survives
context compaction. Verification rules live in [`VERIFICATION.md`](VERIFICATION.md); this file is the loop around
them.

## Roles

- **Coordinator** (one long-lived Claude session): picks the next slice, writes the prompt, reads results, decides,
  commits or pushes, and keeps the handoff file current. It does not implement except for small fixes and for
  diagnosis delegated agents have already failed at once.
- **Workers**: one per slice. Implementer, then a separate adversarial reviewer, then a fixer. Every review in the
  0.11 work found real defects, so the reviewer is never skipped.

## The loop, per slice

1. Slice the task: one module or interface, named files, named tests, the `verify.sh` stages to run.
2. Write the prompt to `<scratchpad>/pi/<task>.md` (template below) and run the worker in the background.
3. Read only the tail of the worker's log and `artifacts/verify/result.json`, never its full transcript.
4. Run the reviewer on the commit range, read its findings, write a fix prompt covering every finding.
5. When `result.json` passes for the touched stages: push, then confirm CI with
   `gh run watch <id> --exit-status` in the background. CI failing is a stop-everything signal.
6. Update the handoff file (below), then start the next slice.

## Choosing Codex or Claude for a worker

Check quota with `agent-usage.sh` (in `~/projects/scripts`; it reads `/status` and `/usage` through temporary herdr
panes) at the start of a session and whenever a 5-hour window is close to its limit.

| Situation | Worker |
|---|---|
| Default: implementation slices, reviews, fixes | Codex via `pi -p --model openai-codex/gpt-6-sol --thinking low --no-session "$(cat prompt.md)"` |
| Codex 5-hour window under 15% left | Claude subagent (`Agent`, `model: sonnet`, `effort: low`) until the Codex window resets |
| Both windows low (Codex under 15%, Claude session over 70%) | Pause and resume after the earlier reset |
| Performance tuning, Fable runtime internals, concurrency diagnosis, after one failed Codex attempt | The coordinator (Claude) directly |
| Claude weekly usage over 60% | No Claude subagents; Codex only, pausing at Codex limits |

Never run Codex and a Claude worker on the same tree at once: workers squash with `git reset --soft`, and a
concurrent commit gets folded into the wrong squash. Parallel work needs its own worktree.

## Keeping the coordinator's context small

- Tool output goes to files; read `tail` or `grep` slices.
- Prompts are files, so a retry reuses them instead of retyping.
- Do not paste worker transcripts or full diffs into the conversation; read the review's findings file.

## Surviving compaction: the handoff file

The coordinator keeps `<scratchpad>/HANDOFF.md` current after every step. After a compaction or in a new session,
read it first, then `git log --oneline -10`, `grep '^- \[ \] ' dev-docs/TASKS.md`, and `gh run list --limit 3`.
It holds only what the repository does not:

- the slice in flight: its background job, prompt file, log file, and checkpoint `restore=` sha;
- the queue order for this session, when it differs from `TASKS.md` order;
- decisions waiting on the user;
- the last quota reading and when each window resets;
- open flaky-test leads not yet in `VERIFICATION.md`.

Durable lessons go into this file, `VERIFICATION.md`, or the coordinator's memory, not into the handoff file.

## Prompt template

```text
Axial F# repo (current directory, git worktree on main; stay on main). Read AGENTS.md and dev-docs/VERIFICATION.md.
Run `scripts/checkpoint.sh "<label>"` and note the restore= sha. <SLICE N of TASK>: <one-sentence intent>.
Implement it; partial progress is better than none.

1..n. Concrete steps naming files, signatures, and behaviour.
Tests: one [<Fact>] per named behaviour.
Docs and the release note; remove the task from TASKS.md when the last slice lands.
`scripts/verify.sh <stages>`; report the summary lines.
Squash: `git reset --soft <restore sha>` and commit "<message>". Do not push.
```

Reviewer prompts say "Do NOT edit files", name the commit range, list the claims to attack, and ask for numbered
findings with file:line, a failure scenario, a fix, and CONFIRMED or PLAUSIBLE.
