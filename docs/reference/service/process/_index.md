---
title: "Services Process"
weight: 50
---

This page shows the external-process service package. Immutable `ProcessSpec` values describe safely tokenized commands, connected topologies, I/O routing, and execution policy. `Process.run` composes the selected `IProcess` interpreter into the current Flow runtime; `Process.stream` emits output incrementally.

## Model

- [`Flow.Process.ProcessSpec`](./t-flow-process-processspec.md):  An immutable description of one command or a connected process topology and its execution policy.
- [`Flow.Process.ProcessPlan`](./t-flow-process-processplan.md):  A redacted, serializable description of work that would be executed.
- [`Flow.Process.InputSource`](./t-flow-process-inputsource.md):  Supplies bytes to the first process stage.
- [`Flow.Process.OutputTarget`](./t-flow-process-outputtarget.md):  Receives bytes from a process topology. Capture limits are measured in bytes.
- [`Flow.Process.ProcessResult`](./t-flow-process-processresult.md):  The complete structured transcript returned by a process execution.
- [`Flow.Process.StageResult`](./t-flow-process-stageresult.md):  The redacted command, exit decision, timing, and diagnostic stderr tail for one stage.
- [`Flow.Process.CapturedOutput`](./t-flow-process-capturedoutput.md):  Exact captured bytes plus their decoded text view and truncation status.
- [`Flow.Process.ProcessOutput`](./t-flow-process-processoutput.md):  A timestamped decoded output event attributed to one specification stage.
- [`Flow.Process.ProcessEvent`](./t-flow-process-processevent.md):  Values emitted by a native process FlowStream.
- [`Flow.Process.ProcessStartFailure`](./t-flow-process-processstartfailure.md):  Diagnostic details for a process that could not be started.
- [`Flow.Process.ProcessTimeout`](./t-flow-process-processtimeout.md):  Diagnostic details for an elapsed process deadline.
- [`Flow.Process.ProcessCancellation`](./t-flow-process-processcancellation.md):  Diagnostic details for caller-initiated process cancellation.
- [`Flow.Process.StageFailure`](./t-flow-process-stagefailure.md):  Diagnostic details for an unsuccessful process stage.
- [`Flow.Process.ProcessIoFailure`](./t-flow-process-processiofailure.md):  Diagnostic details for a process I/O failure.
- [`Flow.Process.ProcessError`](./t-flow-process-processerror.md):  A recoverable process startup, cancellation, stage, or I/O failure.

## Service

- [`Flow.Process.IProcess`](./t-flow-process-iprocess.md):  Interprets process specifications as lazy Axial workflows for a concrete host platform.

## Errors

- [`Flow.Process.ProcessError.describe`](./m-flow-process-processerror-describe.md):  Formats a process error with stage-aware diagnostic context.
 <example><code>error |&gt; ProcessError.describe</code></example>
- [`Flow.Process.ProcessError.exitCode`](./m-flow-process-processerror-exitcode.md):  Returns a suitable host exit code for a process failure.
 <example><code>Environment.ExitCode &lt;- ProcessError.exitCode error</code></example>

## Commands

- [`Flow.Process.command`](./m-flow-process-process-command.md):  Creates a runnable, safely tokenized one-command process specification.
 <example><code>Process.command "git" [ "status"; "--short" ] |&gt; Process.run</code></example>
- [`Flow.Process.arg`](./m-flow-process-process-arg.md):  Appends one ordinary argument.
 <example><code>command |&gt; Process.arg "--verbose"</code></example>
- [`Flow.Process.secretArg`](./m-flow-process-process-secretarg.md):  Adds an argument whose value is replaced with <c>***</c> in rendered commands and transcripts.
- [`Flow.Process.workingDirectory`](./m-flow-process-process-workingdirectory.md):  Sets the working directory. <example><code>command |&gt; Process.workingDirectory repo</code></example>
- [`Flow.Process.environment`](./m-flow-process-process-environment.md):  Sets an environment override. <example><code>command |&gt; Process.environment "CI" "true"</code></example>
- [`Flow.Process.removeEnvironment`](./m-flow-process-process-removeenvironment.md):  Removes an inherited environment variable. <example><code>command |&gt; Process.removeEnvironment "TOKEN"</code></example>
- [`Flow.Process.encoding`](./m-flow-process-process-encoding.md):  Selects text decoding for this stage. <example><code>command |&gt; Process.encoding Encoding.Latin1</code></example>
- [`Flow.Process.successCodes`](./m-flow-process-process-successcodes.md):  Replaces the set of exit codes considered successful for this command.
- [`Flow.Process.render`](./m-flow-process-process-render.md):  Renders a redacted shell-like description of the complete process specification.

## Composition

- [`Flow.Process.pipe`](./m-flow-process-process-pipe.md):  Connects the current stdout to the next one-command specification's stdin.
- [`Flow.Process.pipeBoth`](./m-flow-process-process-pipeboth.md):  Connects both stdout and stderr from the current final stage to the next command's stdin.
- [`Flow.Process.merge`](./m-flow-process-process-merge.md):  Creates a fan-in topology whose producers may be connected to one downstream command.
- [`Flow.Process.stdin`](./m-flow-process-process-stdin.md):  Supplies stdin to the first stage.
- [`Flow.Process.stdout`](./m-flow-process-process-stdout.md):  Configures final stdout handling. <example><code>specification |&gt; Process.stdout OutputTarget.Console</code></example>
- [`Flow.Process.stderr`](./m-flow-process-process-stderr.md):  Configures combined stderr handling. <example><code>specification |&gt; Process.stderr (OutputTarget.CaptureTail 65536)</code></example>
- [`Flow.Process.mergeStderr`](./m-flow-process-process-mergestderr.md):  Routes final stderr through the final stdout targets, like the intent of <c>2&gt;&amp;1</c>.
- [`Flow.Process.framing`](./m-flow-process-process-framing.md):  Selects chunk or line event framing. <example><code>specification |&gt; Process.framing OutputFraming.Lines</code></example>
- [`Flow.Process.timeout`](./m-flow-process-process-timeout.md):  Sets the maximum execution time for the complete process topology.
 <example><code>specification |&gt; Process.timeout (TimeSpan.FromSeconds 30.0)</code></example>
- [`Flow.Process.plan`](./m-flow-process-process-plan.md):  Returns a redacted execution plan without starting a process.

## Execution

- [`Flow.Process.run`](./m-flow-process-process-run.md):  Runs a process specification in the current Flow runtime.
 <example><code>specification |&gt; Process.run</code></example>
- [`Flow.Process.capture`](./m-flow-process-process-capture.md):  Runs a process specification with complete stdout and stderr capture.
 <example><code>Process.command "dotnet" [ "--info" ] |&gt; Process.capture</code></example>
- [`Flow.Process.stream`](./m-flow-process-process-stream.md):  Streams process events in the current Flow runtime. The last event is <c>Completed</c>.
- [`Flow.Process.Script.run`](./m-flow-process-script-run.md):  Runs a process workflow with live services, writes failures through the supplied console, and returns a host exit code.

## Input endpoints

- [`Flow.Process.DSL.Input.empty`](./p-flow-process-dsl-input-empty.md):  Supplies no bytes and closes stdin.
- [`Flow.Process.DSL.Input.text`](./m-flow-process-dsl-input-text.md):  Supplies encoded text incrementally when execution begins.
- [`Flow.Process.DSL.Input.bytes`](./m-flow-process-dsl-input-bytes.md):  Supplies exact bytes when execution begins.
- [`Flow.Process.DSL.Input.file`](./m-flow-process-dsl-input-file.md):  Streams bytes from a file when execution begins.
- [`Flow.Process.DSL.Input.read`](./m-flow-process-dsl-input-read.md):  Reads one asynchronous byte block when execution begins.
- [`Flow.Process.DSL.Input.produce`](./m-flow-process-dsl-input-produce.md):  Produces asynchronous byte blocks with backpressure.
- [`Flow.Process.DSL.Input.stream`](./m-flow-process-dsl-input-stream.md):  Adapts a .NET stream into a backpressured input source.

## Output endpoints

- [`Flow.Process.DSL.Output.capture`](./p-flow-process-dsl-output-capture.md):  Retains all output bytes and their decoded text view.
- [`Flow.Process.DSL.Output.captureTail`](./m-flow-process-dsl-output-capturetail.md):  Retains only the final maximum number of bytes.
- [`Flow.Process.DSL.Output.console`](./p-flow-process-dsl-output-console.md):  Forwards redirected bytes through the host console streams.
- [`Flow.Process.DSL.Output.inheritHandles`](./p-flow-process-dsl-output-inherithandles.md):  Gives the child the host handle directly; this channel cannot also be observed or teed.
- [`Flow.Process.DSL.Output.discard`](./p-flow-process-dsl-output-discard.md):  Drains and discards output.
- [`Flow.Process.DSL.Output.file`](./m-flow-process-dsl-output-file.md):  Writes output to a truncating file.
- [`Flow.Process.DSL.Output.appendFile`](./m-flow-process-dsl-output-appendfile.md):  Writes output to an appending file.
- [`Flow.Process.DSL.Output.callback`](./m-flow-process-dsl-output-callback.md):  Sends exact byte chunks to an asynchronous backpressured callback.
- [`Flow.Process.DSL.Output.tee`](./m-flow-process-dsl-output-tee.md):  Sends every byte chunk to each target in order.
- [`Flow.Process.DSL.Output.stream`](./m-flow-process-dsl-output-stream.md):  Adapts a writable .NET stream into an output target.
- [`Flow.Process.DSL.Output.textWriter`](./m-flow-process-dsl-output-textwriter.md):  Decodes output incrementally into a .NET text writer.

## Concise DSL

- [`Flow.Process.DSL.cmd`](./m-flow-process-dsl-cmd.md):  Builds a command-line-shaped command while preserving every interpolation hole as one argument.
- [`Flow.Process.DSL.cmdText`](./m-flow-process-dsl-cmdtext.md):  Parses a fixed command line. Prefer <c>cmd $"...{value}"</c> whenever values are inserted.
- [`Flow.Process.DSL.pipe`](./m-flow-process-dsl-pipe.md):  Builds a vertical specification from safely parsed command templates.
- [`Flow.Process.DSL.pipeTo`](./m-flow-process-dsl-pipeto.md):  Connects stdout from a command or specification to the next command's stdin.
- [`Flow.Process.DSL.pipeBothTo`](./m-flow-process-dsl-pipebothto.md):  Connects both stdout and stderr from the current final stage to the next command.
- [`Flow.Process.DSL.merge`](./m-flow-process-dsl-merge.md):  Creates line-framed fan-in producers ready to connect to one consumer.
- [`Flow.Process.DSL.mergeBytes`](./m-flow-process-dsl-mergebytes.md):  Creates raw-byte fan-in producers with explicitly nondeterministic chunk interleaving.
- [`Flow.Process.DSL.mergeStderr`](./m-flow-process-dsl-mergestderr.md):  Routes final stderr through final stdout targets.
- [`Flow.Process.DSL.cwd`](./m-flow-process-dsl-cwd.md):
- [`Flow.Process.DSL.env`](./m-flow-process-dsl-env.md):
- [`Flow.Process.DSL.stdin`](./m-flow-process-dsl-stdin.md):  Supplies a primary input source to a command or specification.
- [`Flow.Process.DSL.stdout`](./m-flow-process-dsl-stdout.md):  Configures final stdout on the specification.
- [`Flow.Process.DSL.stderr`](./m-flow-process-dsl-stderr.md):  Configures combined stderr on the specification.
- [`Flow.Process.DSL.timeout`](./m-flow-process-dsl-timeout.md):  Sets the maximum execution time for a command or specification.
 <example><code>cmd $"service-device" |&gt; timeout (TimeSpan.FromSeconds 30.0) |&gt; capture</code></example>
- [`Flow.Process.DSL.run`](./m-flow-process-dsl-run.md):  Runs a command or specification in the current Flow runtime.
- [`Flow.Process.DSL.capture`](./m-flow-process-dsl-capture.md):  Runs a command or specification and captures stdout and stderr.
- [`Flow.Process.DSL.console`](./m-flow-process-dsl-console.md):  Forwards stdout and stderr to the host console while retaining structured completion data.
- [`Flow.Process.DSL.stream`](./m-flow-process-dsl-stream.md):  Produces a bounded stream of structured output and completion events.
- [`Flow.Process.DSL.writeTo`](./m-flow-process-dsl-writeto.md):  Writes final stdout to a truncating file and runs the specification.
- [`Flow.Process.DSL.appendTo`](./m-flow-process-dsl-appendto.md):  Writes final stdout to an appending file and runs the specification.
- [`Flow.Process.DSL.captureParallel`](./m-flow-process-dsl-captureparallel.md):  Captures commands concurrently with a fixed upper bound while preserving input order.

## Shells

- [`Flow.Process.DSL.bash`](./m-flow-process-dsl-bash.md):  Builds a Bash program with interpolation values passed as positional arguments.
- [`Flow.Process.DSL.sh`](./m-flow-process-dsl-sh.md):  Builds a POSIX shell program with interpolation values passed as positional arguments.
- [`Flow.Process.DSL.pwsh`](./m-flow-process-dsl-pwsh.md):  Builds a PowerShell 7 program with interpolation values passed through <c>$args</c>.
- [`Flow.Process.DSL.bashText`](./m-flow-process-dsl-bashtext.md):  Builds an explicitly assembled Bash program. Values in the text are not escaped by Axial.
- [`Flow.Process.DSL.shText`](./m-flow-process-dsl-shtext.md):  Builds an explicitly assembled POSIX shell program. Values in the text are not escaped by Axial.
- [`Flow.Process.DSL.pwshText`](./m-flow-process-dsl-pwshtext.md):  Builds an explicitly assembled PowerShell 7 program. Values in the text are not escaped by Axial.
- [`Flow.Process.DSL.secret`](./m-flow-process-dsl-secret.md):  Marks an interpolated value for diagnostic redaction.

## Implementations

- [`Flow.Process.live`](./m-flow-process-process-live.md):  Creates a live process service using an explicit clock for transcript timestamps and durations.
- [`Flow.Process.layer`](./m-flow-process-process-layer.md):  Builds a live process service from an explicit clock as a layer.
