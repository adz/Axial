---
title: Processes
description: Start external commands from an Axial workflow.
---

# Processes

`Axial.Process` lets a command-line program start an external command without giving up typed failures, cancellation,
or cleanup. You describe the command first, choose what happens to its input and output, then run it.

## Start with a script

For a small command-line program, describe commands, connect them, choose where output goes, then call `run`:

```fsharp no-check reason="Runs the locally installed dotnet command through live services"
open Axial.Process
open Axial.Process.DSL // Short pipeline helpers: cwd, env, timeout, =>, capture, run

Process.command $"git log --oneline -20"
|> cwd repository
|> env "NO_COLOR" "1"
|> timeout (TimeSpan.FromSeconds 5)
=> cmd $"head -5"
|> capture
|> run
```

[`Process.command`](xref:M:Axial.Process.Process.command) (and DSL [`cmd`](xref:M:Axial.Process.DSL.cmd)) preserves each interpolation hole as one native argument. [`cwd`](xref:M:Axial.Process.DSL.cwd), [`env`](xref:M:Axial.Process.DSL.env), and
[`timeout`](xref:M:Axial.Process.DSL.timeout) return updated command specifications. [`=>`](xref:M:Axial.Process.DSL.(=>)) connects stdout to the next command's stdin. [`capture`](xref:M:Axial.Process.DSL.capture) creates
a `Flow` that retains stdout and stderr; none of those steps starts a child process. The final [`run`](xref:M:Axial.Process.DSL.run) starts the Flow
with live clock, filesystem, console, and process services. It returns `0` on success, or writes a redacted error and
returns an appropriate nonzero exit code. Use it at a .NET command-line application's outermost boundary.

For a server, worker, or application Flow, see [run a process in an application Flow](running-in-an-application-flow.html).
That guide explains the explicit process capability, live wiring, test fakes, and the difference between [`Process.toFlow`](xref:M:Axial.Process.Process.toFlow)
and [`Flow.run`](xref:M:Axial.Flow.run).

## Choose how the command communicates

| Need | DSL | Full API | What you receive |
| --- | --- | --- | --- |
| Inspect output after the command exits | [`capture`](xref:M:Axial.Process.DSL.capture) | [`Process.capture`](xref:M:Axial.Process.Process.capture) | `ProcessResult`: stdout, stderr, bytes, exit codes, and timing |
| Show output as it arrives | [`console`](xref:M:Axial.Process.DSL.console) | [`Process.console`](xref:M:Axial.Process.Process.console) | Structured completion data |
| React to output before completion | [`stream`](xref:M:Axial.Process.DSL.stream) | [`Process.stream`](xref:M:Axial.Process.Process.stream) | A backpressured stream of output and completion events |
| Send final output elsewhere | [`writeTo`](xref:M:Axial.Process.DSL.writeTo) / [`appendTo`](xref:M:Axial.Process.DSL.appendTo) | [`Process.stdout`](xref:M:Axial.Process.Process.stdout) / [`Process.stderr`](xref:M:Axial.Process.Process.stderr) | A configured command |

New process specifications capture stdout and stderr by default. [`capture`](xref:M:Axial.Process.DSL.capture) makes that choice explicit and replaces a prior output policy. [Output capture and destinations](output-capture.html) explains bounded capture, files, console forwarding, and tees. [Streaming output](streaming.html) covers long-running commands and incremental handling.

## What Axial owns

The process service starts the native topology; Axial owns its lifetime. A timeout or interruption terminates every started stage, including partially started pipelines, before the enclosing Flow finishes. Expected process problems remain `ProcessError` values instead of becoming unstructured exceptions.

## Guides

- [Commands, input, and pipelines](composition.html)
- [Output capture and destinations](output-capture.html)
- [Streaming output](streaming.html)
- [Failures and transcripts](failures-and-transcripts.html)
- [Scripts and shell syntax](scripts.html)
- [Worked examples](worked-examples.html)
- [Run a process in an application Flow](running-in-an-application-flow.html)
- [Fable](fable.html)
