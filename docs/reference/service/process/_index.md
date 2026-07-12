---
title: "Services Process"
weight: 50
---

This page shows the external-process service package. Immutable `Command` and `Pipeline` values preserve shell-like endpoint composition without shell parsing. `Process.toFlow` converts a topology through the explicit `IProcess` capability, connects real standard streams, captures complete output, tracks every exit code, and reports startup, cancellation, I/O, and non-zero exits through `ProcessError`. Use `Process.stream` when output must be observed before completion.

## Model

- [`Flow.Process.Command`](./t-flow-process-command.md):  An immutable, safely tokenized external command.
- [`Flow.Process.Pipeline`](./t-flow-process-pipeline.md):  One or more commands connected left-to-right through their real standard streams.
- [`Flow.Process.ProcessPlan`](./t-flow-process-processplan.md):  A redacted, serializable description of work that would be executed.
- [`Flow.Process.InputSource`](./t-flow-process-inputsource.md):  Supplies bytes to the first process stage.
- [`Flow.Process.OutputTarget`](./t-flow-process-outputtarget.md):  Receives bytes from a process topology. Capture limits are measured in bytes.
- [`Flow.Process.ProcessResult`](./t-flow-process-processresult.md):  The complete structured transcript returned by a process execution.
- [`Flow.Process.StageResult`](./t-flow-process-stageresult.md):  The redacted command, exit decision, timing, and diagnostic stderr tail for one stage.
- [`Flow.Process.CapturedOutput`](./t-flow-process-capturedoutput.md):  Exact captured bytes plus their decoded text view and truncation status.
- [`Flow.Process.ProcessOutput`](./t-flow-process-processoutput.md):  A timestamped decoded output event attributed to one pipeline stage.
- [`Flow.Process.ProcessEvent`](./t-flow-process-processevent.md):  Values emitted by a native process FlowStream.
- [`Flow.Process.ProcessError`](./t-flow-process-processerror.md):  A recoverable process startup, cancellation, stage, or I/O failure.

## Service

- [`Flow.Process.IProcess`](./t-flow-process-iprocess.md):  Executes typed process pipelines for a concrete host platform.

## Errors

- [`Flow.Process.ProcessError.describe`](./m-flow-process-processerror-describe.md):  Formats a process error with stage-aware diagnostic context.
 <example><code>error |&gt; ProcessError.describe</code></example>
- [`Flow.Process.ProcessError.exitCode`](./m-flow-process-processerror-exitcode.md):  Returns a suitable host exit code for a process failure.
 <example><code>Environment.ExitCode &lt;- ProcessError.exitCode error</code></example>

## Commands

- [`Flow.Process.command`](./m-flow-process-process-command.md):  Creates a safely tokenized command.
 <example><code>Process.command "git" [ "status"; "--short" ]</code></example>
- [`Flow.Process.arg`](./m-flow-process-process-arg.md):  Appends one ordinary argument.
 <example><code>command |&gt; Process.arg "--verbose"</code></example>
- [`Flow.Process.secretArg`](./m-flow-process-process-secretarg.md):  Adds an argument whose value is replaced with <c>***</c> in rendered commands and transcripts.
- [`Flow.Process.workingDirectory`](./m-flow-process-process-workingdirectory.md):  Sets the working directory. <example><code>command |&gt; Process.workingDirectory repo</code></example>
- [`Flow.Process.environment`](./m-flow-process-process-environment.md):  Sets an environment override. <example><code>command |&gt; Process.environment "CI" "true"</code></example>
- [`Flow.Process.removeEnvironment`](./m-flow-process-process-removeenvironment.md):  Removes an inherited environment variable. <example><code>command |&gt; Process.removeEnvironment "TOKEN"</code></example>
- [`Flow.Process.encoding`](./m-flow-process-process-encoding.md):  Selects text decoding for this stage. <example><code>command |&gt; Process.encoding Encoding.Latin1</code></example>
- [`Flow.Process.successCodes`](./m-flow-process-process-successcodes.md):  Replaces the set of exit codes considered successful for this command.
- [`Flow.Process.render`](./m-flow-process-process-render.md):  Renders a diagnostic command string with secret arguments redacted.

## Pipelines

- [`Flow.Process.pipeline`](./m-flow-process-process-pipeline.md):  Starts a one-command pipeline with full stdout and stderr capture.
 <example><code>command |&gt; Process.pipeline</code></example>
- [`Flow.Process.pipe`](./m-flow-process-process-pipe.md):  Connects the current stdout to the next command's stdin. <example><code>pipeline |&gt; Process.pipe next</code></example>
- [`Flow.Process.pipeBoth`](./m-flow-process-process-pipeboth.md):  Connects both stdout and stderr from the current final stage to the next command's stdin.
- [`Flow.Process.merge`](./m-flow-process-process-merge.md):  Creates a fan-in topology whose producers may be connected to one downstream command.
- [`Flow.Process.stdin`](./m-flow-process-process-stdin.md):  Supplies stdin to the first stage.
- [`Flow.Process.stdout`](./m-flow-process-process-stdout.md):  Configures final stdout handling. <example><code>pipeline |&gt; Process.stdout OutputTarget.Console</code></example>
- [`Flow.Process.stderr`](./m-flow-process-process-stderr.md):  Configures combined stderr handling. <example><code>pipeline |&gt; Process.stderr (OutputTarget.CaptureTail 65536)</code></example>
- [`Flow.Process.mergeStderr`](./m-flow-process-process-mergestderr.md):  Routes final stderr through the final stdout targets, like the intent of <c>2&gt;&amp;1</c>.
- [`Flow.Process.framing`](./m-flow-process-process-framing.md):  Selects chunk or line event framing. <example><code>pipeline |&gt; Process.framing OutputFraming.Lines</code></example>
- [`Flow.Process.renderPipeline`](./m-flow-process-process-renderpipeline.md):  Renders a redacted shell-like diagnostic pipeline. <example><code>Process.renderPipeline pipeline</code></example>
- [`Flow.Process.plan`](./m-flow-process-process-plan.md):  Returns a redacted execution plan without starting a process.

## Execution

- [`Flow.Process.toFlow`](./m-flow-process-process-toflow.md):  Converts a topology to Flow and fails on the first unsuccessful stage.
 <example><code>pipeline |&gt; Process.toFlow</code></example>
- [`Flow.Process.toFlowResult`](./m-flow-process-process-toflowresult.md):  Converts a topology to Flow without interpreting stage success policies.
 <example><code>pipeline |&gt; Process.toFlowResult</code></example>
- [`Flow.Process.observe`](./m-flow-process-process-observe.md):  Converts a topology to Flow with an asynchronous observer and validates stage success policies.
 <example><code>pipeline |&gt; Process.observe observer</code></example>
- [`Flow.Process.observeResult`](./m-flow-process-process-observeresult.md):  Converts a topology to Flow with an asynchronous observer and without interpreting stage success policies.
 <example><code>pipeline |&gt; Process.observeResult observer</code></example>
- [`Flow.Process.stream`](./m-flow-process-process-stream.md):  Streams structured process events with one-element bounded backpressure. The last event is <c>Completed</c>.
- [`Flow.Process.execute`](./m-flow-process-process-execute.md):  Creates and runs one command with default capture policy.
 <example><code>Process.execute "dotnet" [ "--version" ]</code></example>
- [`Flow.Process.Script.run`](./m-flow-process-script-run.md):  Runs a process workflow with live services and sets the host exit code. Intended for dotnet-fsi shebang scripts.

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
- [`Flow.Process.DSL.pipe`](./m-flow-process-dsl-pipe.md):  Builds a vertical pipeline from safely parsed command templates.
- [`Flow.Process.DSL.pipeCommands`](./m-flow-process-dsl-pipecommands.md):  Builds a pipeline from already constructed commands.
- [`Flow.Process.DSL.pipeTo`](./m-flow-process-dsl-pipeto.md):  Connects stdout from a command or pipeline to the next command's stdin.
- [`Flow.Process.DSL.pipeBothTo`](./m-flow-process-dsl-pipebothto.md):  Connects both stdout and stderr from the current final stage to the next command.
- [`Flow.Process.DSL.merge`](./m-flow-process-dsl-merge.md):  Creates line-framed fan-in producers ready to connect to one consumer.
- [`Flow.Process.DSL.mergeBytes`](./m-flow-process-dsl-mergebytes.md):  Creates raw-byte fan-in producers with explicitly nondeterministic chunk interleaving.
- [`Flow.Process.DSL.mergeStderr`](./m-flow-process-dsl-mergestderr.md):  Routes final stderr through final stdout targets.
- [`Flow.Process.DSL.cwd`](./m-flow-process-dsl-cwd.md):
- [`Flow.Process.DSL.env`](./m-flow-process-dsl-env.md):
- [`Flow.Process.DSL.stdin`](./m-flow-process-dsl-stdin.md):  Supplies a primary input source to a command or pipeline.
- [`Flow.Process.DSL.stdout`](./m-flow-process-dsl-stdout.md):  Configures final stdout without converting the topology.
- [`Flow.Process.DSL.stderr`](./m-flow-process-dsl-stderr.md):  Configures combined stderr without converting the topology.
- [`Flow.Process.DSL.toFlow`](./m-flow-process-dsl-toflow.md):  Explicitly converts a command or pipeline into a captured Flow.
- [`Flow.Process.DSL.capture`](./m-flow-process-dsl-capture.md):  Waits for completion and captures stdout and stderr.
- [`Flow.Process.DSL.captureResult`](./m-flow-process-dsl-captureresult.md):  Captures output without interpreting command success codes.
- [`Flow.Process.DSL.console`](./m-flow-process-dsl-console.md):  Forwards stdout and stderr to the host console while retaining structured completion data.
- [`Flow.Process.DSL.stream`](./m-flow-process-dsl-stream.md):  Produces a bounded stream of structured output and completion events.
- [`Flow.Process.DSL.writeTo`](./m-flow-process-dsl-writeto.md):  Writes final stdout to a truncating file and converts the topology to Flow.
- [`Flow.Process.DSL.appendTo`](./m-flow-process-dsl-appendto.md):  Writes final stdout to an appending file and converts the topology to Flow.
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
