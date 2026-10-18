---
title: Process operations
api:
  family: Axial.Process.Process
  sections:
    - id: create
      title: Create commands
      order: 10
      summary: Turn templates or argument lists into immutable command descriptions.
      members: [command, commandArgs, commandText, fileName, arguments, arg, secretArg]
    - id: configure
      title: Configure execution
      order: 20
      summary: Set stage defaults, environment, working directory, timeouts, and success codes before running.
      members: [workingDirectory, tryWorkingDirectory, environment, environmentVariables, removeEnvironment, encoding, successCodes, timeout, withoutTimeout, overallTimeout, drainTimeout, stageDefaults, mapFinalCommand]
    - id: compose
      title: Compose commands
      order: 30
      summary: Connect commands into process topologies and combine independent specifications.
      members: [pipe, connect, pipeStdoutAndStderr, mergeSpecifications, mergeStdout, mergeStderr, ofCommand]
    - id: route
      title: Route input and output
      order: 40
      summary: Choose where each native process channel reads and writes.
      members: [stdinFrom, stdinFromText, stdinFromBytes, stdinFromFile, stdinFromInheritHandles, stdoutTo, stdoutToFile, stdoutToAppendFile, stdoutToConsole, stdoutToDiscard, stdoutToInheritHandles, stderrTo, stderrToFile, stderrToAppendFile, stderrToConsole, stderrToDiscard, stderrToInheritHandles]
    - id: capture
      title: Capture output
      order: 50
      summary: Retain all, a bounded tail, or none of each readable output channel.
      members: [captureAll, captureTail, captureNothing, captureStderrAll, captureStderrTail, captureStderrNothing, setCapture]
    - id: execute
      title: Execute workflows
      order: 60
      summary: Choose lazy Flow composition or a direct script boundary.
      members: [toFlow, toFlowSpecification, run, runWith, tryRun, tryRunWith, unwrapRun, service]
    - id: inspect
      title: Inspect plans
      order: 70
      summary: Resolve and render a redacted execution plan without starting a process.
      members: [plan, planSpecification, render, renderPlan]
    - id: stream
      title: Stream output
      order: 80
      summary: Produce completion-aware process output streams for incremental consumption.
      members: [lineStream, streamOutputs, streamStdout, streamStderr, streamStdoutLines, streamStderrLines]
    - id: host
      title: Provide and test the host
      order: 90
      summary: Create a live host or substitute the process service in tests.
      members: [createHost, live, liveHost, test]
---

# Process operations


A command or specification is immutable data. Constructing it starts no process. Use `toFlow` to add execution to an
application workflow, or `run` / `tryRun` for a script boundary. The former raises `ProcessRunException` on failure;
the latter preserves the complete `Exit` so the caller can choose a policy.

Output routing and retention are separate decisions. A destination controls where bytes go; capture controls which
bytes remain available in `ProcessResult`. Streaming constructors produce a `ProcessStream`, whose transforms and
terminals are grouped on its companion module page.

See [Commands and composition](/process/composition.html), [output capture](/process/output-capture.html), and
[streaming output](/process/streaming.html) for examples.