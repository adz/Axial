---
title: Command DSL
api:
  family: Axial.Process.DSL
  sections:
    - id: construct
      title: Construct commands
      order: 10
      summary: Tokenize command templates or fixed text without implicitly invoking a shell.
      members: [cmd, cmdText, secret]
    - id: connect
      title: Connect commands
      order: 20
      summary: Build process pipelines from commands or specifications.
      members: [pipe, pipeTo, pipeStdoutAndStderrTo, (=>)]
    - id: shell
      title: Invoke a shell explicitly
      order: 30
      summary: Use a named shell when the command needs shell syntax.
      members: [bash, bashText, sh, shText, pwsh, pwshText]
    - id: configure
      title: Configure stages
      order: 40
      summary: Set command, environment, input, timeout, and output policies.
      members: [cwd, env, timeout, withoutTimeout, overallTimeout, drainTimeout, stageDefaults, framing, maxLineLength, mapFinalCommand]
    - id: route
      title: Route process channels
      order: 50
      summary: Supply stdin and direct stdout or stderr to destinations.
      members: [stdinFrom, stdinFromText, stdinFromBytes, stdinFromFile, stdinFromInheritHandles, stdoutTo, stdoutToFile, stdoutToAppendFile, stdoutToConsole, stdoutToDiscard, stdoutToInheritHandles, stderrTo, stderrToFile, stderrToAppendFile, stderrToConsole, stderrToDiscard, stderrToInheritHandles]
    - id: capture
      title: Capture output
      order: 60
      summary: Choose bounded or complete capture independently for stdout and stderr.
      members: [captureAll, captureTail, captureNothing, captureStderrAll, captureStderrTail, captureStderrNothing]
    - id: stream
      title: Stream process output
      order: 70
      summary: Construct output streams and explicitly merge channels when needed.
      members: [streamOutputs, streamStdout, streamStderr, streamStdoutLines, streamStderrLines, mergeStderr, mergeStdout, mergeStdoutBytes]
    - id: execute
      title: Execute and combine
      order: 80
      summary: Convert process descriptions into Flows or capture independent commands in parallel.
      members: [toFlow, captureParallel, captureParallelSpecifications]
---

# Command DSL

The `Axial.Process.DSL` module offers short names and operators for constructing process
specifications. Open it when command-shaped syntax reads better:

```fsharp
open Axial.Process.DSL

let countLines =
    cmd $"printf 'one\\ntwo\\n'"
    => cmd $"wc -l"
```

`cmd` tokenizes an interpolated template without invoking a shell. Each interpolation value remains one native
argument. `=>` connects stdout to the next command's stdin. Shells are explicit: use [`bash`](#Axial.Process.DSL.bash),
[`sh`](#Axial.Process.DSL.sh), or [`pwsh`](#Axial.Process.DSL.pwsh) when shell syntax is actually needed.

The DSL functions are aliases for the corresponding `Process` operations; they do not change
execution semantics. Use [`cmdText`](#Axial.Process.DSL.cmdText) for fixed text that should be tokenized, or
`Process.commandArgs` when the executable and arguments are already separate values.

See [Commands and composition](/process/composition.html) for the short path and
[Scripts and shell syntax](/process/scripts.html) for command-line programs.
