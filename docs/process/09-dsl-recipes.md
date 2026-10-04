---
title: DSL recipes
description: Checked recipes for input, routing, fan-in, parallel commands, and workflow composition.
---

Open `Axial.Process.DSL` to build commands with `cmd`, configure them with pipeline helpers, and connect them
with `=>`. Configuration is lazy; `toFlow`, streaming terminals, and host runners choose how to execute it.
`ProcessStream` and `Flow` combinators stay in their own modules, following the type they operate on.

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

```fsharp
// Real processes, with an injected console for checked examples.
let host = ProcessHostEnvironment.liveWithConsole (Console.test())
let resultOf (workflow: Flow<ProcessHostEnvironment, ProcessError, ProcessResult>) =
    match workflow |> runWith host with
    | Exit.Success result -> result
    | other -> failwithf "Expected process success, got %A" other
```

## Supply stdin and connect stages

Text, bytes, and file shortcuts configure one command's stdin. `Input.text`, `Input.bytes`, and `Input.file`
provide equivalent endpoints for `=>`. A connected stage receives its predecessor's output; it cannot also
have an explicit stdin source.

```fsharp run
let upper =
    Input.text "hello\n" => cmd $"tr '[:lower:]' '[:upper:]'"
    => cmd $"cat"
    |> toFlow |> resultOf
shouldEqual "HELLO\n" upper.StdOut

cmd $"cat" |> stdinFromBytes [| 65uy; 66uy |] |> toFlow |> resultOf
|> _.StdOut |> shouldEqual "AB"

cmd $"cat" |> stdinFromText "inline" |> toFlow |> resultOf
|> _.StdOut |> shouldEqual "inline"
```

Use `Input.readOnce` for one asynchronously obtained byte block, or `Input.produce` to write several blocks
with backpressure. The producer runs during execution and must cooperate with cancellation.

```fsharp run
cmd $"cat"
|> stdinFrom (Input.readOnce (fun () -> async { return Encoding.UTF8.GetBytes "once" }))
|> toFlow |> resultOf |> _.StdOut |> shouldEqual "once"

let generated = Input.produce (fun write -> async {
    do! write (Encoding.UTF8.GetBytes "first\n")
    do! write (Encoding.UTF8.GetBytes "second\n")
})
generated => cmd $"cat" |> toFlow |> resultOf |> _.StdOut
|> shouldEqual "first\nsecond\n"
```

## Write files, append, and tee

Routing and capture are independent: `stdoutToFile` still captures stdout unless you choose `captureNothing`.
Configure a final command's route before connecting it, or use `mapFinalCommand` on an existing topology.
An `Output` endpoint with `=>` configures a single command; it does not execute it.

```fsharp run
let inputPath = Path.GetTempFileName()
let outputPath = Path.GetTempFileName()
let errorPath = Path.GetTempFileName()
try
    File.WriteAllText(inputPath, "source\n")
    let copied =
        cmd $"cat" |> stdinFromFile inputPath |> stdoutToFile outputPath
        |> toFlow |> resultOf
    shouldEqual "source\n" copied.StdOut
    shouldEqual "source\n" (File.ReadAllText outputPath)

    cmd $"printf appended" |> stdoutToAppendFile outputPath |> captureNothing
    |> toFlow |> resultOf |> _.StdOut |> shouldEqual ""
    shouldEqual "source\nappended" (File.ReadAllText outputPath)

    sh $"printf warning >&2" |> stderrToFile errorPath |> captureStderrAll
    |> toFlow |> resultOf |> _.StdErr |> shouldEqual "warning"
    sh $"printf again >&2" |> stderrToAppendFile errorPath
    |> toFlow |> resultOf |> ignore
    shouldEqual "warningagain" (File.ReadAllText errorPath)

    use writer = new StringWriter()
    let teed =
        cmd $"printf duplicated" => Output.tee [Output.file outputPath; Output.textWriter Encoding.UTF8 writer]
        |> captureTail 4 |> toFlow |> resultOf
    shouldEqual "duplicated" (File.ReadAllText outputPath)
    shouldEqual "duplicated" (writer.ToString())
    shouldEqual "ated" teed.StdOut
finally
    File.Delete inputPath
    File.Delete outputPath
    File.Delete errorPath
```

`Output.callback` receives exact bytes, while `Output.stream` and `Output.textWriter` adapt caller-owned .NET
objects. Axial flushes adapters without disposing those objects; a text-writer decoder is fresh for each execution.

```fsharp run
do
    use source = new MemoryStream(Encoding.UTF8.GetBytes "streamed")
    use destination = new MemoryStream()
    cmd $"cat" |> stdinFrom (Input.stream source) |> stdoutTo (Output.stream destination)
    |> captureNothing |> toFlow |> resultOf |> ignore
    shouldEqual "streamed" (Encoding.UTF8.GetString(destination.ToArray()))
    shouldEqual true destination.CanWrite

    let chunks = ResizeArray<byte>()
    cmd $"printf callback" |> stdoutTo (Output.callback (fun bytes -> async { chunks.AddRange bytes }))
    |> captureNothing |> toFlow |> resultOf |> ignore
    shouldEqual "callback" (Encoding.UTF8.GetString(chunks.ToArray()))
```

`stdoutToConsole` and `stderrToConsole` forward redirected bytes through the injected console. The
`*ToInheritHandles` variants give the child native handles directly; inherited channels cannot be captured,
observed, or teed. `stdoutToDiscard` and `stderrToDiscard` drain without forwarding; capture remains independent.

```fsharp run
let captured = cmd $"printf retained" |> stdoutToDiscard |> toFlow |> resultOf
shouldEqual "retained" captured.StdOut
let quiet = sh $"printf warning >&2" |> stderrToDiscard |> captureStderrNothing |> toFlow |> resultOf
shouldEqual "" quiet.StdErr
```

## Keep stderr separate or explicitly merge it

Batch output defaults to full stdout and separate stderr tails. `mergeStderr` joins final stderr into the
observed stdout channel. `pipeStdoutAndStderrTo` instead feeds both native channels into the next command.
Order between independently written channels is not guaranteed.

```fsharp run
let separated = sh $"printf data; printf warning >&2" |> toFlow |> resultOf
shouldEqual "data" separated.StdOut
shouldEqual "warning" separated.StdErr

let merged = sh $"printf 'data\n'; printf 'warning\n' >&2" |> mergeStderr |> toFlow |> resultOf
shouldEqual ["data"; "warning"] (merged.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> Array.sort |> Array.toList)
shouldEqual "" merged.StdErr

sh $"printf 'data\n'; printf 'warning\n' >&2"
|> pipeStdoutAndStderrTo (cmd $"sort") |> toFlow |> resultOf |> _.StdOut
|> shouldEqual "data\nwarning\n"
```

## Fan in producers or capture independent commands

`mergeStdout` combines producers at line boundaries before a downstream consumer. Relative producer order
is unspecified, so sort when the result needs a stable order. `mergeStdoutBytes` forwards byte chunks without
line framing; use it when line preservation is unnecessary, rather than for independently structured binary files.

```fsharp run
let ordered =
    [cmd $"printf 'beta\n'"; cmd $"printf 'alpha\n'"]
    |> mergeStdout |> pipeTo (cmd $"sort") |> toFlow |> resultOf
shouldEqual "alpha\nbeta\n" ordered.StdOut

let bytes =
    [cmd $"printf A"; cmd $"printf B"]
    |> mergeStdoutBytes |> pipeTo (cmd $"cat") |> toFlow |> resultOf
shouldEqual [65uy; 66uy] (bytes.StdOutCapture.Bytes |> Array.sort |> Array.toList)

let concurrentCaptures = [cmd $"printf first"; cmd $"printf second"] |> captureParallel 2
match concurrentCaptures |> runWith host with
| Exit.Success results -> shouldEqual ["first"; "second"] (results |> List.map _.StdOut)
| other -> failwithf "Expected parallel capture, got %A" other
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
    |> mapFinalCommand (captureTail 3)
let plan = Process.plan pipeline
shouldEqual (Some(TimeSpan.FromSeconds 5.)) plan.Stages[0].Timeout
shouldEqual None plan.Stages[1].Timeout
shouldEqual "command" plan.Stages[1].Provenance["timeout"]
pipeline |> toFlow |> resultOf |> _.StdOut |> shouldEqual "ady"
```

Use qualified command helpers where the DSL has no shortcut, such as `Process.successCodes`,
`Process.removeEnvironment`, `Process.encoding`, and `Process.commandArgs`.

```fsharp run
let accepted = sh $"exit 3" |> Process.successCodes [0; 3] |> toFlow |> resultOf
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
sequential |> runWith host |> shouldEqual (Exit.Success "first:second")

let recovered =
    sh $"exit 7" |> toFlow
    |> Flow.orElseWith (fun _ -> cmd $"printf fallback" |> toFlow)
recovered |> resultOf |> _.StdOut |> shouldEqual "fallback"
```

Use [`streaming output`](streaming.html) for completion-aware transformations and effectful consumers,
and [`scripts`](scripts.html) for safe shell interpolation and the `run` / `runExitCode` boundary.
