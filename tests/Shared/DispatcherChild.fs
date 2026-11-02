namespace Axial.Tests.Shared

open System
open System.Diagnostics
open System.IO
open System.Text
open System.Threading.Tasks

/// Runs a dispatcher scenario in a bounded child process. The scenario is an F# script body compiled against the
/// supplied assemblies; the prelude supplies a single-threaded queue context, an acknowledged-gate helper, and
/// hard exits for failed assertions so a stranded worker cannot keep the suite alive.
module DispatcherChild =
    let private prelude = """
open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Axial

type QueueContext() =
    inherit SynchronizationContext()
    let queue = ConcurrentQueue<SendOrPostCallback * obj>()
    let available = new AutoResetEvent(false)
    let mutable posted = 0
    member _.Posted = Volatile.Read(&posted)
    override _.Post(callback, state) =
        Interlocked.Increment(&posted) |> ignore
        queue.Enqueue((callback, state))
        available.Set() |> ignore
    member _.PumpOne(timeout: TimeSpan) =
        match queue.TryDequeue() with
        | true, (callback, state) ->
            callback.Invoke state
            true
        | _ ->
            available.WaitOne(timeout) |> ignore
            match queue.TryDequeue() with
            | true, (callback, state) ->
                callback.Invoke state
                true
            | _ -> false

let fail message =
    eprintfn "ASSERTION FAILED: %s" message
    Environment.Exit 5

let check condition message = if not condition then fail message

// Test-only observation of Task's registered await continuation. The child runs on the pinned .NET SDK.
let continuationField = typeof<Task>.GetField("m_continuationObject", System.Reflection.BindingFlags.Instance ||| System.Reflection.BindingFlags.NonPublic)
let hasSubscription (pending: Task) =
    check (not (isNull continuationField)) "Task continuation observation is unavailable"
    not (isNull (continuationField.GetValue pending))

// Hold an early-notifying factory open until the worker has checked the subscription. This turns the
// factory-notification race into a deterministic assertion failure instead of a scheduling-dependent pass.
let returnPending (signal: TaskCompletionSource<unit>) (observed: ManualResetEventSlim) (pending: Task<'a>) =
    if signal.Task.IsCompleted then
        check (observed.Wait(TimeSpan.FromSeconds 5.0)) "worker did not acknowledge the factory signal"
    pending

let signalAfterSubscription (pending: Task) (signal: TaskCompletionSource<unit>) =
    Task.Run(fun () ->
        check (SpinWait.SpinUntil((fun () -> hasSubscription pending), TimeSpan.FromSeconds 5.0))
            "runner did not subscribe to the pending task"
        check (not pending.IsCompleted) "task completed before subscription was acknowledged"
        signal.TrySetResult() |> ignore)

type FinalizerFunc(onInvoke: unit -> unit, pending: Task) =
    inherit Microsoft.FSharp.Core.FSharpFunc<CancellationToken, Task>()
    override _.Invoke _ =
        onInvoke ()
        pending

/// Starts `run` inside a dispatcher callback on its owner thread. The owner pumps only until the callback returns,
/// so a continuation posted by the runner is observable as an incomplete run; the outer deadline terminates the
/// child instead of leaving a blocked thread behind.
let runOnDispatcher (run: unit -> 'a) (checkResult: 'a -> int -> bool -> unit) =
    let context = QueueContext()
    let result = TaskCompletionSource<'a * int * bool>(TaskCreationOptions.RunContinuationsAsynchronously)
    let thread =
        Thread(
            (fun () ->
                SynchronizationContext.SetSynchronizationContext context
                try
                    context.Post(
                        (fun _ ->
                            try
                                let before = context.Posted
                                let value = run ()
                                let restored = Object.ReferenceEquals(SynchronizationContext.Current, context)
                                result.TrySetResult((value, context.Posted - before, restored)) |> ignore
                            with error ->
                                result.TrySetException error |> ignore),
                        null
                    )
                    let deadline = DateTime.UtcNow.AddSeconds 5.0
                    while not result.Task.IsCompleted && DateTime.UtcNow < deadline do
                        context.PumpOne(TimeSpan.FromMilliseconds 20.0) |> ignore
                finally
                    SynchronizationContext.SetSynchronizationContext null),
            IsBackground = true
        )
    thread.Start()
    if not (result.Task.Wait(TimeSpan.FromSeconds 8.0)) then
        eprintfn "RUNNER DID NOT RETURN: %d Post calls remain queued" context.Posted
        Environment.Exit 3
    let value, posts, restored = result.Task.Result
    checkResult value posts restored

let suspended (run: Flow<unit, string, int> -> Exit<int, string>) (expect: Exit<int, string> -> unit) =
    let subscribed = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
    let gate = TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)
    let cleanupStarted = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
    let cleanupGate = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
    use bodyObserved = new ManualResetEventSlim(false)
    use cleanupObserved = new ManualResetEventSlim(false)
    let bodySubscription = signalAfterSubscription gate.Task subscribed
    let cleanupSubscription = signalAfterSubscription cleanupGate.Task cleanupStarted
    let mutable prefixThread = 0
    let workflow =
        flow {
            prefixThread <- Thread.CurrentThread.ManagedThreadId
            do!
                Flow.scopeFinalizer (
                    Flow.fromTask (fun _ ->
                        returnPending cleanupStarted cleanupObserved cleanupGate.Task)
                )
            let! value =
                Flow.fromTask (fun _ ->
                    returnPending subscribed bodyObserved gate.Task)
            return value + 1
        }
    let worker =
        Task.Run(fun () ->
            (task {
                do! subscribed.Task
                let bodySubscribed = hasSubscription gate.Task
                bodyObserved.Set()
                check bodySubscribed "body gate released before Flow.fromTask subscribed"
                gate.TrySetResult 41 |> ignore
                do! cleanupStarted.Task
                let cleanupSubscribed = hasSubscription cleanupGate.Task
                cleanupObserved.Set()
                check cleanupSubscribed "cleanup gate released before Flow.fromTask subscribed"
                cleanupGate.TrySetResult() |> ignore
            } :> Task))
    runOnDispatcher
        (fun () ->
            let ownerThread = Thread.CurrentThread.ManagedThreadId
            run workflow, ownerThread)
        (fun (exit, ownerThread) posts restored ->
            worker.GetAwaiter().GetResult()
            bodySubscription.GetAwaiter().GetResult()
            cleanupSubscription.GetAwaiter().GetResult()
            expect exit
            check (posts = 0) (sprintf "flow execution posted %d dispatcher continuations" posts)
            check restored "the original synchronization context was not restored"
            check (ownerThread = prefixThread) "the synchronous prefix did not start on the owner thread")

let direct (run: unit -> Exit<int, string>) (expect: Exit<int, string> -> unit) =
    runOnDispatcher run (fun exit posts restored ->
        expect exit
        check (posts = 0) (sprintf "flow execution posted %d dispatcher continuations" posts)
        check restored "the original synchronization context was not restored")

let expectSuccess value (exit: Exit<int, string>) =
    match exit with
    | Exit.Success actual -> check (actual = value) (sprintf "expected Success %d, got Success %d" value actual)
    | other -> fail (sprintf "expected Success %d, got %A" value other)

let expectTyped error (exit: Exit<int, string>) =
    match exit with
    | Exit.Failure(Cause.Fail actual) -> check (actual = error) (sprintf "expected Fail %s, got Fail %s" error actual)
    | other -> fail (sprintf "expected typed failure %s, got %A" error other)

let expectDefect (exit: Exit<int, string>) =
    match exit with
    | Exit.Failure(Cause.Die _) -> ()
    | other -> fail (sprintf "expected defect, got %A" other)

let expectInterrupt (exit: Exit<int, string>) =
    match exit with
    | Exit.Failure Cause.Interrupt -> ()
    | other -> fail (sprintf "expected interruption, got %A" other)

printfn "PRELUDE READY"
"""

    /// Runs the scenario in a child `dotnet fsi` process. Returns the exit code and combined output.
    let run (assemblies: string list) (scenario: string) (timeout: TimeSpan) : int * string =
        let scriptPath = Path.Combine(Path.GetTempPath(), $"axial-dispatcher-{Guid.NewGuid():N}.fsx")
        let references = assemblies |> List.map (fun path -> $"#r \"{path}\"") |> String.concat "\n"
        File.WriteAllText(scriptPath, references + "\n" + prelude + "\n" + scenario + "\nprintfn \"SCENARIO DONE\"\n")
        try
            use child =
                new Process(
                    StartInfo =
                        ProcessStartInfo(
                            FileName = "dotnet",
                            Arguments = $"fsi --nologo --quiet \"{scriptPath}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false
                        )
                )
            child.Start() |> ignore
            let output = child.StandardOutput.ReadToEndAsync()
            let errors = child.StandardError.ReadToEndAsync()
            if not (child.WaitForExit(int timeout.TotalMilliseconds)) then
                try child.Kill(entireProcessTree = true) with _ -> ()
                Task.WaitAll([| output :> Task; errors :> Task |], TimeSpan.FromSeconds 5.0) |> ignore
                124, output.Result + errors.Result + "\nchild process did not finish before the deadline"
            else
                Task.WaitAll([| output :> Task; errors :> Task |], TimeSpan.FromSeconds 5.0) |> ignore
                child.ExitCode, output.Result + errors.Result
        finally
            try File.Delete scriptPath with _ -> ()
