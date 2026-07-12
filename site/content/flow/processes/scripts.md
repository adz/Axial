---
weight: 40
title: Executable Scripts
description: Run typed Flow workflows from one dotnet-fsi shebang file.
type: docs
---


This page shows complete executable `.fsx` scripts that provision the live process service with one call.

## Minimal Shebang Script

```fsharp
#!/usr/bin/env -S dotnet fsi
#r "nuget: Axial.Flow.Process, 0.7.0"

open Axial.Flow.Process
open Axial.Flow.Process.DSL

cmd $"dotnet --version"
|> console
|> Script.run
```

Make the file executable and run it:

```bash
chmod +x versions.fsx
./versions.fsx
```

Example output:

```text
10.0.300
```

`Script.run` is the interpreter boundary: it supplies `Process.live Clock.live`, runs the Flow, and sets the host exit code. A
failed stage propagates its exit code, cancellation uses 130, and startup or I/O failures use one.

## Full Capture Script

```fsharp
#!/usr/bin/env -S dotnet fsi
#r "nuget: Axial.Flow.Process, 0.7.0"

open Axial.Flow
open Axial.Flow.Process
open Axial.Flow.Process.DSL

flow {
    let! branch = cmd $"git branch --show-current" |> capture
    let! tests = cmd $"dotnet test --configuration Release" |> capture
    printfn "branch=%s tests=%O" (branch.StdOut.Trim()) tests.Duration
}
|> Script.run
```

## Full Streaming Script

```fsharp
#!/usr/bin/env -S dotnet fsi
#r "nuget: Axial.Flow.Process, 0.7.0"

open Axial.Flow
open Axial.Flow.Process
open Axial.Flow.Process.DSL

let print = function
    | ProcessEvent.Output output ->
        flow {
            let prefix = if output.Channel = OutputChannel.StdErr then "error" else "output"
            printf "[%s:%d] %s" prefix output.Stage output.Text
        }
    | ProcessEvent.Completed result ->
        flow { printfn "completed %d stages in %O" result.Stages.Length result.Duration }

cmd $"dotnet build --nologo"
=> cmd $"tee build.log"
|> Process.framing OutputFraming.Lines
|> stream
|> FlowStream.runForEachFlow print
|> Script.run
```

Use a compiled application when startup time, deployment, richer dependency layers, or several source files matter.
The shebang host is for focused automation where one file is the useful unit of distribution.
