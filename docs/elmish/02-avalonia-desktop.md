---
title: Avalonia desktop application
linkTitle: Avalonia desktop
description: Start an Elmish runtime in an Avalonia app, run services, debounce UI input, stream results, shut down, and add a --self-test path.
project: src/Axial.ElmishFlows/Axial.ElmishFlows.fsproj
---

# Avalonia desktop application

This recipe wires one Elmish program to Axial in an Avalonia desktop application, from startup to exit. The view
binding is yours to choose (Avalonia.FuncUI, an Elmish.Avalonia binding, or your own view model); the examples use
FuncUI's `Program.withHost`, and the Axial parts are the same for every binding.

`Axial.Hosting` is for Generic Host, ASP.NET Core, systemd services and console exit codes; a GUI does not need it,
except for the `--self-test` path below, which is a console run.

```fsharp prepare
open System
open Axial
open Axial.ElmishFlows
open Axial.Process
open Elmish

let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

The Axial parts of this recipe are compiled and run as part of the documentation; only the blocks that touch Avalonia
are not.

## The environment

Everything the program's commands and services need lives in one record, built by a layer. The queue and hub are
created inside the runtime's root scope, so they live until the runtime stops:

```fsharp
type SearchRequest = { Id: int64; Query: string }
type SearchEvent =
    | Matches of id: int64 * paths: string list
    | Finished of id: int64

type AppEnv =
    { Process: IProcess
      Requests: Queue<SearchRequest>
      Events: Hub<SearchEvent> }
    interface IHasProcess with
        member this.Process = this.Process

let makeEnvironment (processes: IProcess) : Flow<unit, Never, AppEnv> =
    flow {
        let! requests = Queue.sliding 1
        let! events = Hub.make ()
        return { Process = processes; Requests = requests; Events = events }
    }

let layer (processes: IProcess) : Layer<unit, Never, AppEnv> = Layer.fromFlow (makeEnvironment processes)
```

## Run a service for the program's lifetime

The search pipeline reads requests from the queue and publishes events to the hub. It belongs to the runtime, not to
`init`: `ElmishRuntime.startService` forks it once in the root scope, and stopping the runtime interrupts it.
`FlowStream.switchMapFlow` interrupts the running search when a newer request arrives:

```fsharp
module Search =
    let private runOne (request: SearchRequest) : Flow<AppEnv, Never, unit> =
        flow {
            let! env = Flow.env
            let publish event = Hub.publish event env.Events |> Flow.map ignore
            do! publish (Matches(request.Id, [ $"{request.Query}.log" ]))
            do! publish (Finished request.Id)
        }

    let pipeline : Flow<AppEnv, Never, unit> =
        flow {
            let! env = Flow.env
            return!
                FlowStream.fromDequeue env.Requests
                |> FlowStream.switchMapFlow runOne
                |> FlowStream.runDrain
        }
```

The program sees the hub through a subscription, `Sub.OfFlowStream.subscribe [ "search-events" ] runtime
(FlowStream.fromHub (QueueStrategy.Sliding 64) runtime.Environment.Events) Never.absurd`, so every event reaches
`update` through `Post`.

## UI input, debounce and a window that can change

A text box posts into an `Inbox` from `update` with `Inbox.offer`, which returns a command, so `update` stays pure.
A subscription debounces the inbox. Put the debounce window in the subscription id: when the user changes the window
in settings, the id changes, Elmish disposes the old subscription and starts a new one on the same inbox, and later
input settles with the new window.

```fsharp
type Model = { Query: string; Debounce: TimeSpan; Results: string list; Status: string }

type Msg =
    | QueryChanged of string
    | QuerySettled of string
    | DebounceChanged of TimeSpan
    | Found of string list
    | SearchDone
    | SearchFailed of string
    | RevealFolder of string
    | RevealFailed of ProcessError

let init () : Model * Cmd<Msg> =
    { Query = ""; Debounce = TimeSpan.FromMilliseconds 300.0; Results = []; Status = "" }, Cmd.none

let subscriptions runtime (inbox: Inbox<string>) (model: Model) : Sub<Msg> =
    let window = model.Debounce
    let settled : FlowStream<AppEnv, Never, Msg> =
        Inbox.stream inbox |> FlowStream.debounce window |> FlowStream.map QuerySettled
    Sub.OfFlowStream.subscribe [ "query"; string window.TotalMilliseconds ] runtime settled Never.absurd
```

Values still in the inbox survive the switch. The value the old debounce was holding for its quiet period is dropped,
because that subscription is interrupted; the next keystroke settles normally.

## Stream results into update

When a query settles, `Cmd.OfFlowStream.latest` runs the search as a stream of batches and dispatches each batch as a
message. A newer query with the same key interrupts the older stream, and the batch it left waiting in `Post` is
dropped. The stream waits for `update` to see each batch before it pulls the next, so a fast search cannot flood the UI
dispatcher. `Process.shellOpen` opens a folder in the file manager without waiting for it. `Cmd.OfFlow.attempt`
dispatches nothing when it succeeds and turns its typed failure into a message:

```fsharp
let private searchFiles (query: string) : FlowStream<AppEnv, string, string> =
    FlowStream.fromSeq [ $"{query}-1.log"; $"{query}-2.log" ]

let update runtime inbox (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | QueryChanged text -> { model with Query = text }, Inbox.offer text inbox
    | QuerySettled text ->
        let batches =
            searchFiles text
            |> FlowStream.groupedWithin 64 (TimeSpan.FromMilliseconds 100.0)
            |> FlowStream.map Found
            |> FlowStream.append (FlowStream.singleton SearchDone)
        { model with Results = []; Status = "searching" },
        Cmd.OfFlowStream.latest "search" runtime batches id SearchFailed
    | Found paths -> { model with Results = model.Results @ paths }, Cmd.none
    | SearchDone -> { model with Status = "done" }, Cmd.none
    | SearchFailed error -> { model with Status = error }, Cmd.none
    | DebounceChanged window -> { model with Debounce = window }, Cmd.none
    | RevealFolder folder ->
        model, Cmd.OfFlow.attempt "reveal" runtime (Process.shellOpen folder) RevealFailed
    | RevealFailed error -> { model with Status = ProcessError.describe error }, Cmd.none
```

## Test update with the test runtime

`ElmishRuntime.test` runs the commands `update` returns against a `ManualClock` and the test process service, and
records what they dispatch. The same `update` drives the real program:

```fsharp run
let processes = Process.test ()
let environment =
    match makeEnvironment processes |> Flow.run () with
    | Exit.Success environment -> environment
    | _ -> failwith "environment failed"

let test = ElmishRuntime.test (ManualClock(DateTimeOffset.UnixEpoch)) environment
let inbox = Inbox.create 64 test.Runtime
let model, _ = init ()
let runUpdate msg =
    let _, command = update test.Runtime inbox msg model
    TestRuntime.run command test
    TestRuntime.settle (TimeSpan.FromSeconds 5.0) test |> shouldEqual true

runUpdate (QuerySettled "app")
TestRuntime.dispatched test |> shouldEqual [ box (Found [ "app-1.log"; "app-2.log" ]); box SearchDone ]

// A reveal that succeeds dispatches nothing; the launch is recorded instead of started.
runUpdate (RevealFolder "/var/log")
TestRuntime.dispatched test |> List.length |> shouldEqual 2
match List.ofSeq processes.Launches with
| [ ProcessLaunch.ShellOpen folder ] -> folder |> shouldEqual "/var/log"
| _ -> failwith "expected one shell open"

let notInstalled = ProcessError.StartFailed { Command = "xdg-open"; Message = "not installed" }
processes.LaunchHandler <- Some(fun _ -> Flow.fail notInstalled)
runUpdate (RevealFolder "/var/log")
TestRuntime.dispatched test |> List.last |> shouldEqual (box (RevealFailed notInstalled))
TestRuntime.defects test |> shouldEqual []
(test :> IDisposable).Dispose()
```

## Start the runtime when Avalonia is ready

Create the runtime in `OnFrameworkInitializationCompleted`, then the program, and pass the runtime to `init`, `update`
and `subscriptions`. `Post` needs only Avalonia's UI dispatcher, which exists by then; it does not need the Elmish
program or the view binding, because each command captures Elmish's `dispatch` itself. So there is nothing to attach
later: do not create the runtime as a module-level value with a placeholder `Post` that is patched once the UI starts.

```fsharp no-check reason="Avalonia is not referenced by the documentation example compiler; this block is host glue only"
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.FuncUI.Elmish
open Avalonia.Threading
open Axial.Console
open Axial.FileSystem

/// Runs inline when already on the UI thread, so a keystroke dispatches, updates and renders in one pass.
let postToUi (callback: unit -> unit) =
    if Dispatcher.UIThread.CheckAccess() then callback ()
    else Dispatcher.UIThread.Post(fun () -> callback ())

let startRuntime () : ElmishRuntime<AppEnv> =
    let options =
        { Post = postToUi
          OnDefect = fun defect -> eprintfn "defect in %s: %s" defect.Name defect.Rendered
          HistoryCapacity = 128
          Clock = None
          LogSink = None }
    let processes = Process.live FileSystem.live Console.live
    // The one blocking wait, at the desktop host boundary.
    match ElmishRuntime.startLayer options (layer processes) () |> Async.RunSynchronously with
    | Ok runtime -> runtime
    | Error never -> Never.absurd never

type App() =
    inherit Application()

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            let runtime = startRuntime ()
            // A fresh runtime runs no services and is not stopping, so neither error occurs here.
            match runtime |> ElmishRuntime.startService "search-pipeline" Search.pipeline with
            | Ok () -> ()
            | Error ServiceStartError.DuplicateName -> eprintfn "search pipeline already running"
            | Error ServiceStartError.RuntimeStopping -> eprintfn "runtime stopped during startup"
            let inbox = Inbox.create 64 runtime
            let window = MainWindow()
            Program.mkProgram init (update runtime inbox) view
            |> Program.withSubscription (subscriptions runtime inbox)
            |> Program.withHost window
            |> Program.run
            // Closing the window stops services, commands and subscriptions and runs their finalizers.
            desktop.ShutdownRequested.Add(fun _ -> runtime.StopAndWait(TimeSpan.FromSeconds 5.0) |> ignore)
            desktop.MainWindow <- window
        | _ -> ()
        base.OnFrameworkInitializationCompleted()
```

`startLayer` is asynchronous; the one blocking wait sits at the host boundary, as in
[synchronous desktop startup](/elmish/elmish.html#synchronous-desktop-startup), and provisioning must not wait for the
UI dispatcher it is blocking. `StopAndWait` on the UI thread is safe while the finalizers do not need the dispatcher.

## Keep update pure when results need time

`update` must not read the clock. Measure inside the flow, and let the message carry the timing: `Clock.timed`
returns the value and the monotonic duration, and `Clock.now` the wall-clock instant, both from the runtime's clock.
`update` stores what it is given, and a test can install a deterministic clock:

```fsharp
type Loaded = { Lines: string list; Took: TimeSpan; At: DateTimeOffset }

let loadLog : Flow<unit, string, string list> = Flow.succeed [ "started"; "ready" ]

// What Cmd.OfFlow.either runs; update receives the Loaded record and never touches a clock.
let loadTimed : Flow<unit, string, Loaded> =
    flow {
        let! lines, took = loadLog |> Clock.timed
        let! at = Clock.now
        return { Lines = lines; Took = took; At = at }
    }

let describe (loaded: Loaded) =
    $"{loaded.Lines.Length} lines in {loaded.Took.TotalMilliseconds} ms"

/// A clock whose monotonic timer advances 40 ms per reading.
let steppingClock () =
    let mutable readings = 0
    { new IClock with
        member _.UtcNow() = DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero)
        member _.Elapsed() =
            readings <- readings + 1
            TimeSpan.FromMilliseconds(40.0 * float readings)
        member _.Sleep(_, _) = System.Threading.Tasks.Task.CompletedTask }
```

```fsharp run
match loadTimed |> Flow.withClock (steppingClock ()) |> Flow.run () with
| Exit.Success loaded ->
    describe loaded |> shouldEqual "2 lines in 40 ms"
    loaded.At |> shouldEqual (DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero))
| _ -> failwith "load failed"
```

## A --self-test command-line path

A `--self-test` argument runs the same layer and services without a window, and reports a process exit code, so CI
can smoke-test the packaged application. `DotNetApp.exitCode` from `Axial.Hosting` maps the final `Exit` to `0`
(success), `1` (typed failure), `2` (defect) or `130` (interruption):

```fsharp
open Axial.Hosting

module SelfTest =
    let private check : Flow<AppEnv, string, unit> =
        flow {
            let! env = Flow.env
            let! subscription = env.Events |> Hub.subscribe QueueStrategy.Unbounded
            let! _ = env.Requests |> Queue.offer { Id = 1L; Query = "self-test" }
            let! first = Dequeue.take subscription
            match first with
            | Matches(1L, _ :: _) -> ()
            | _ -> return! Flow.fail "search returned no matches"
        }

    let runWith (processes: IProcess) : int =
        let application =
            flow {
                let! _ = Flow.fork Search.pipeline
                return! check |> Flow.timeout (TimeSpan.FromSeconds 10.0) "self-test timed out"
            }
            |> Layer.provide (layer processes |> Layer.widenError)
        let exit = application |> Flow.run ()
        match exit with
        | Exit.Failure cause -> eprintfn "%s" (Cause.prettyPrint id cause)
        | Exit.Success () -> ()
        DotNetApp.exitCode exit
```

```fsharp run
SelfTest.runWith (Process.test ()) |> shouldEqual 0
```

The entry point picks the path; only this part needs Avalonia:

```fsharp no-check reason="Avalonia is not referenced by the documentation example compiler; this block is host glue only"
[<EntryPoint>]
let main args =
    if Array.contains "--self-test" args then
        SelfTest.runWith (Process.live FileSystem.live Console.live)
    else AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args)
```

For the program's own logic, drive `init` and `update` with `ElmishRuntime.test` and a `ManualClock`, as in
[Test update with the test runtime](#test-update-with-the-test-runtime); the self-test proves the packaged services
start.
