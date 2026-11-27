---
title: Guardrail diagnostics
linkTitle: Diagnostics
description: One page per Guardrails rule, with the problem it prevents, code it reports, and the fix.
---

# Guardrail diagnostics

Each rule has a name and a code. A finding reads:

```text
warning AXG001: raw-effect: Replace File.ReadAllText with …
```

You write the name in allowances, notes and the TOML. The code is what editors, CI, `NoWarn` and
`WarningsAsErrors` use.

| Rule | Code | The problem | Kind |
| --- | --- | --- | --- |
| [`raw-effect`](raw-effect.html) | AXG001 | A clock, file, random or other effect that a test cannot replace | Level set per role |
| [`stale-allow`](stale-allow.html) | AXG002 | An allowance or note that is malformed or no longer needed | Error |
| [`raise-in-flow`](raise-in-flow.html) | AXG003 | An exception inside `flow { }` that callers cannot handle as an error | Error |
| [`shared-fixture`](shared-fixture.html) | AXG004 | A test value shared by every test in a module | Error |
| [`dropped-cancel`](dropped-cancel.html) | AXG005 | A task adapter that ignores its cancellation token | Error |
| [`reflection-format`](reflection-format.html) | AXG006 | Formatting that breaks under NativeAOT and trimming | Error |
| [`fiber-resource`](fiber-resource.html) | AXG007 | A fiber that returns a resource it has already released | Error |
| [`escaped-scope`](escaped-scope.html) | AXG008 | A queue, hub or cache returned from the run that shut it down | Error |
| [`raw-concurrency`](raw-concurrency.html) | AXG009 | A raw `Task` or thread that nothing owns | Block |
| [`swallowed-exn`](swallowed-exn.html) | AXG010 | A catch-all handler that throws the exception away | Guide |
| [`cancel-none`](cancel-none.html) | AXG011 | `CancellationToken.None`, which nothing can cancel | Block |
| [`dropped-work`](dropped-work.html) | AXG012 | A discarded `Task` or fiber | Block |
| [`guessed-wait`](guessed-wait.html) | AXG013 | A test that sleeps instead of waiting for what it checks | Block and guide |
| [`task-surface`](task-surface.html) | AXG014 | Raw `Task` code in domain and service code | Info |

**Block** findings are errors and take no note. **Guide** findings warn and take a
[note](../allowances-and-notes.html#acknowledge-a-guide-finding-with-a-note); without one they are errors at a
checkpoint. **Info** findings never fail a build.

No rule offers an automatic fix except `raise-in-flow` (for a standalone `raise`) and `stale-allow` (removing a
directive on its own line). The others change behaviour, and only you know the right choice.
