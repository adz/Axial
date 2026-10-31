---
title: DSL recipes
description: Checked recipes for input, routing, fan-in, parallel commands, and workflow composition.
---

Open `Axial.Process.DSL` to build commands with `cmd`, configure them with pipeline helpers, and connect them
with `=>`. Configuration is lazy; `toFlow`, streaming terminals, and host runners choose how to execute it.
`ProcessStream` and `Flow` combinators live in the modules of their input types.

```fsharp prepare
open System
open System.IO
open System.Text
open Axial
open Axial.Console
open Axial.FileSystem
open Axial.PlatformService
open Axial.Process
open Axial.Process.DSL

let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

## Supply stdin and connect stages

Text, bytes, and file shortcuts configure one command's stdin. `Input.text`, `Input.bytes`, and `Input.file`
provide equivalent `InputSource` endpoints for `=>`. A connected stage receives its predecessor's output; it cannot also
have an explicit stdin source.

```fsharp run
let upper =
    Input.text "hello\n" => cmd $"tr '[:lower:]' '[:upper:]'"
    => cmd $"cat"
    |> Process.run
shouldEqual "HELLO\n" upper.StdOut

cmd $"cat" |> stdinFromBytes [| 65uy; 66uy |] |> Process.run
|> _.StdOut |> shouldEqual "AB"

cmd $"cat" |> stdinFromText "inline" |> Process.run
|> _.StdOut |> shouldEqual "inline"
```

`Input.flowStream` consumes a cold byte stream with backpressure. The process spec has no environment;
close over application services with `Flow.localEnv` (or `Layer.provide`) before passing the stream to the spec.

```fsharp run
let generated =
    [ "first\n"; "second\n" ]
    |> List.map Encoding.UTF8.GetBytes
    |> FlowStream.fromSeq
    |> Input.flowStream
generated => cmd $"cat" |> Process.run |> _.StdOut
|> shouldEqual "first\nsecond\n"
```

## Write files, append, and tee

Routing and capture are independent: `stdoutToFile` still captures stdout unless you choose `captureNothing`.
Configure a final command's route before connecting it, or use
[`Process.mapFinalCommand`](xref:M:Axial.Process.Process.mapFinalCommand) to adjust the final command on an existing topology.
An `OutputTarget` endpoint built by `Output.*` with `=>` configures a single command; it does not execute it.

```fsharp run
let inputPath = Path.GetTempFileName()
let outputPath = Path.GetTempFileName()
let errorPath = Path.GetTempFileName()
try
    File.WriteAllText(inputPath, "source\n")
    let copied =
        cmd $"cat" |> stdinFromFile inputPath |> stdoutToFile outputPath
        |> Process.run
    shouldEqual "source\n" copied.StdOut
    shouldEqual "source\n" (File.ReadAllText outputPath)

    cmd $"printf appended" |> stdoutToAppendFile outputPath |> captureNothing
    |> Process.run |> _.StdOut |> shouldEqual ""
    shouldEqual "source\nappended" (File.ReadAllText outputPath)

    sh $"printf warning >&2" |> stderrToFile errorPath |> captureStderrAll
    |> Process.run |> _.StdErr |> shouldEqual "warning"
    sh $"printf again >&2" |> stderrToAppendFile errorPath
    |> Process.run |> ignore
    shouldEqual "warningagain" (File.ReadAllText errorPath)

    use writer = new StringWriter()
    let teed =
        cmd $"printf duplicated"
        => Output.tee [ Output.file outputPath; Output.textWriter Encoding.UTF8 writer ]
        |> captureTail 4 |> Process.run
    shouldEqual "duplicated" (File.ReadAllText outputPath)
    shouldEqual "duplicated" (writer.ToString())
    shouldEqual "ated" teed.StdOut
finally
    File.Delete inputPath
    File.Delete outputPath
    File.Delete errorPath
```

The `System.IO.File` calls above are docs-test setup: they create and remove scratch files so the routing output can
be checked. In an application, model the filesystem with `IFileSystem` and a typed `FileSystemError`; see
[FileSystem](/filesystem/filesystem.html).

`Output.callback` receives exact bytes, while `Output.stream` and `Output.textWriter` adapt caller-owned .NET
objects. Axial flushes adapters without disposing those objects; a text-writer decoder is fresh for each execution.

```fsharp run
do
    use source = new MemoryStream(Encoding.UTF8.GetBytes "streamed")
    use destination = new MemoryStream()
    cmd $"cat" |> stdinFrom (Input.stream source) |> stdoutTo (Output.stream destination)
    |> captureNothing |> Process.run |> ignore
    shouldEqual "streamed" (Encoding.UTF8.GetString(destination.ToArray()))
    shouldEqual true destination.CanWrite

    let chunks = ResizeArray<byte>()
    cmd $"printf callback"
    |> stdoutTo (Output.callback (fun bytes -> Flow.fromAsync (async { chunks.AddRange bytes })))
    |> captureNothing |> Process.run |> ignore
    shouldEqual "callback" (Encoding.UTF8.GetString(chunks.ToArray()))
```

`stdoutToConsole` and `stderrToConsole` forward redirected bytes through the injected console. The
`*ToInheritHandles` variants give the child native handles directly; inherited channels cannot be captured,
observed, or teed. `stdoutToDiscard` and `stderrToDiscard` drain without forwarding; capture remains independent.

```fsharp run
let captured = cmd $"printf retained" |> stdoutToDiscard |> Process.run
shouldEqual "retained" captured.StdOut
let quiet =
    sh $"printf warning >&2" |> stderrToDiscard |> captureStderrNothing
    |> Process.run
shouldEqual "" quiet.StdErr
```

## Keep stderr separate or explicitly merge it

Batch output defaults to full stdout and separate stderr tails. `mergeStderr` joins final stderr into the
observed stdout channel. `pipeStdoutAndStderrTo` instead feeds both native channels into the next command.
Order between independently written channels is not guaranteed.

```fsharp run
let separated = sh $"printf data; printf warning >&2" |> Process.run
shouldEqual "data" separated.StdOut
shouldEqual "warning" separated.StdErr

let merged =
    sh $"printf 'data\n'; printf 'warning\n' >&2" |> mergeStderr
    |> Process.run
let words =
    merged.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries)
    |> Array.sort
    |> Array.toList
shouldEqual ["data"; "warning"] words
shouldEqual "" merged.StdErr

sh $"printf 'data\n'; printf 'warning\n' >&2"
|> pipeStdoutAndStderrTo (cmd $"sort") |> Process.run |> _.StdOut
|> shouldEqual "data\nwarning\n"
```

## Fan in producers or capture independent commands

`mergeStdout` combines producers at line boundaries before a downstream consumer. Relative producer order
is unspecified, so sort when the result needs a stable order. `mergeStdoutBytes` forwards byte chunks without
line framing; use it when line preservation is unnecessary, rather than for independently structured binary files.

```fsharp run
let ordered =
    [cmd $"printf 'beta\n'"; cmd $"printf 'alpha\n'"]
    |> mergeStdout |> pipeTo (cmd $"sort") |> Process.run
shouldEqual "alpha\nbeta\n" ordered.StdOut

let bytes =
    [cmd $"printf A"; cmd $"printf B"]
    |> mergeStdoutBytes |> pipeTo (cmd $"cat") |> Process.run
shouldEqual [65uy; 66uy] (bytes.StdOutCapture.Bytes |> Array.sort |> Array.toList)

let concurrentCaptures = [cmd $"printf first"; cmd $"printf second"] |> captureParallel 2
let results = concurrentCaptures |> Process.run
shouldEqual ["first"; "second"] (results |> List.map _.StdOut)
```

`captureParallel` limits concurrent workflows and preserves the input order of results. It does not connect
commands' streams. Homogeneous lists may contain commands or already connected specs.

## Apply defaults and override the final stage

`stageDefaults` supplies fallbacks for every stage, including stages connected later. Explicit settings win;
`withoutTimeout` explicitly disables a fallback timeout. Destination topology defaults win when combining
policies. Environment defaults resolve per key: `env` overrides a fallback, and `Process.removeEnvironment`
removes a key even when the defaults supply it. Use `Process.plan` to inspect resolution and provenance without
starting processes.

```fsharp run
let defaults = StageDefaults.empty |> StageDefaults.timeout (TimeSpan.FromSeconds 5.)
let pipeline =
    cmd $"printf ready" |> stageDefaults defaults
    |> pipeTo (cmd $"cat" |> withoutTimeout)
    |> Process.mapFinalCommand (Process.captureTail 3)
let plan = Process.plan pipeline
shouldEqual (Some(TimeSpan.FromSeconds 5.)) plan.Stages[0].Timeout
shouldEqual None plan.Stages[1].Timeout
shouldEqual "command" plan.Stages[1].Provenance["timeout"]
pipeline |> Process.run |> _.StdOut |> shouldEqual "ady"
```

Use qualified command helpers where the DSL has no shortcut, such as `Process.successCodes`,
`Process.removeEnvironment`, `Process.encoding`, and `Process.commandArgs`.

```fsharp run
let accepted = sh $"exit 3" |> Process.successCodes [0; 3] |> Process.run
shouldEqual 3 accepted.ExitCode
```

## Sequence commands and recover through Flow

`=>` connects native streams; it does not mean “run the next command after success.” Use `flow { }` for
sequential application work and `Flow.orElseWith` for typed recovery. Keep command failures in the application's
normal error channel until the host boundary decides its policy.

```fsharp run
let sequential = flow {
    let! first = cmd $"printf first" |> toFlow
    let! second = cmd $"printf second" |> toFlow
    return first.StdOut + ":" + second.StdOut
}
sequential |> Process.run |> shouldEqual "first:second"

let recovered =
    sh $"exit 7" |> toFlow
    |> Flow.orElseWith (fun _ -> cmd $"printf fallback" |> toFlow)
recovered |> Process.run |> _.StdOut |> shouldEqual "fallback"
```

Use [`streaming output`](streaming.html) for completion-aware transformations and effectful consumers,
and [`scripts`](scripts.html) for safe shell interpolation and the `Process.run` boundary.
