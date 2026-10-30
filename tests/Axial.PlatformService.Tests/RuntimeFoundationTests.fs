namespace Axial.Tests

open System
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.PlatformService
open Swensen.Unquote
open Xunit

module RuntimeFoundationTests =
    type LayerCompositionEnv =
        { A: int
          B: int
          C: int }

    [<Fact>]
    let ``scope finalizers run in reverse order and only once`` () =
        let calls = ResizeArray<string>()

        let scope = new Scope()
        scope.AddFinalizer(fun _ ->
            calls.Add "first"
            Task.CompletedTask)
        scope.AddFinalizer(fun _ ->
            calls.Add "second"
            Task.CompletedTask)
        scope.AddFinalizer(fun _ ->
            calls.Add "third"
            Task.CompletedTask)

        (scope :> IDisposable).Dispose()
        (scope :> IDisposable).Dispose()

        test <@ List.ofSeq calls = [ "third"; "second"; "first" ] @>

    [<Fact>]
    let ``scope aggregates cleanup failures`` () =
        let scope = new Scope()
        scope.AddFinalizer(fun _ -> Task.FromException(InvalidOperationException("first")))
        scope.AddFinalizer(fun _ -> Task.FromException(InvalidOperationException("second")))

        let aggregate =
            try
                scope.Close(CancellationToken.None).GetAwaiter().GetResult()
                failwith "Expected scope cleanup failure."
            with :? AggregateException as error ->
                error

        test <@ aggregate.InnerExceptions.Count = 2 @>
        test <@ aggregate.InnerExceptions[0].Message = "second" @>
        test <@ aggregate.InnerExceptions[1].Message = "first" @>

    [<Fact>]
    let ``scope rejects finalizers after closure`` () =
        let scope = new Scope()
        scope.Close(CancellationToken.None).GetAwaiter().GetResult()

        raises<ObjectDisposedException> <@ scope.AddFinalizer(fun _ -> Task.CompletedTask) @>

    [<Fact>]
    let ``parent scope closes child scopes in deterministic reverse registration order`` () =
        let calls = ResizeArray<string>()
        let parent = new Scope()
        let firstChild = parent.AddChild()
        let secondChild = parent.AddChild()

        firstChild.AddFinalizer(fun _ ->
            calls.Add "first-child"
            Task.CompletedTask)

        secondChild.AddFinalizer(fun _ ->
            calls.Add "second-child"
            Task.CompletedTask)

        parent.Close(CancellationToken.None).GetAwaiter().GetResult()
        parent.Close(CancellationToken.None).GetAwaiter().GetResult()

        test <@ List.ofSeq calls = [ "second-child"; "first-child" ] @>

    [<Fact>]
    let ``provided layer finalizes acquired resources when provisioning fails`` () =
        let calls = ResizeArray<string>()

        let layer : Layer<unit, string, unit> =
            Layer.fromFlow (
                flow {
                    do! Flow.scopeFinalizer (Flow.delay (fun () -> calls.Add "cleanup"; Flow.succeed ()))
                    return! Flow.fail "startup failed"
                }
            )

        let result =
            Flow.succeed "unreachable"
            |> Layer.provide layer
            |> Flow.runSync ()

        test <@ result = Exit.Failure (Cause.Fail "startup failed") @>
        test <@ List.ofSeq calls = [ "cleanup" ] @>

    [<Fact>]
    let ``provided layer finalizes acquired resources when downstream flow fails`` () =
        let calls = ResizeArray<string>()

        let layer : Layer<unit, string, string> =
            Layer.fromFlow (
                flow {
                    do! Flow.scopeFinalizer (Flow.delay (fun () -> calls.Add "cleanup"; Flow.succeed ()))
                    return "resource"
                }
            )

        let result =
            Flow.fail "workflow failed"
            |> Layer.provide layer
            |> Flow.runSync ()

        test <@ result = Exit.Failure (Cause.Fail "workflow failed") @>
        test <@ List.ofSeq calls = [ "cleanup" ] @>

    [<Fact>]
    let ``layer zip provisions sequentially left then right`` () =
        let calls = ResizeArray<string>()

        let left =
            Layer.fromFlow (Flow.delay (fun () -> calls.Add "left"; Flow.succeed 1))

        let right =
            Layer.fromFlow (Flow.delay (fun () -> calls.Add "right"; Flow.succeed 2))

        let result =
            Flow.env<int * int, string>
            |> Layer.provide (Layer.zip left right)
            |> Flow.runSync ()

        test <@ result = Exit.Success (1, 2) @>
        test <@ List.ofSeq calls = [ "left"; "right" ] @>

    [<Fact>]
    let ``layer zipPar starts independent branches and cleans child scopes left then right`` () =
        let calls = ResizeArray<string>()
        let mutable started = 0
        let bothStarted = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

        let makeLayer name value =

            Layer.fromFlow (
                flow {
                    do!
                        Flow.fromTask (fun cancellationToken ->
                            task {
                                calls.Add($"{name}-start")

                                if Interlocked.Increment(&started) = 2 then
                                    bothStarted.TrySetResult() |> ignore

                                use _ = cancellationToken.Register(fun () -> release.TrySetCanceled(cancellationToken) |> ignore)
                                do! release.Task
                            })

                    do! Flow.scopeFinalizer (Flow.delay (fun () -> calls.Add($"{name}-cleanup"); Flow.succeed ()))
                    return value
                }
            )

        let workflow =
            Flow.env<int * int, string>
            |> Layer.provide (Layer.zipPar (makeLayer "left" 1) (makeLayer "right" 2))

        let runTask = Task.Run(fun () -> Flow.runSync () workflow)

        try
            test <@ bothStarted.Task.Wait(TimeSpan.FromSeconds 2.0) @>
        finally
            release.TrySetResult() |> ignore

        let result = runTask.GetAwaiter().GetResult()
        let starts = calls |> Seq.filter (fun call -> call.EndsWith("-start")) |> Set.ofSeq
        let cleanups = calls |> Seq.filter (fun call -> call.EndsWith("-cleanup")) |> List.ofSeq

        test <@ result = Exit.Success (1, 2) @>
        test <@ starts = Set.ofList [ "left-start"; "right-start" ] @>
        test <@ cleanups = [ "left-cleanup"; "right-cleanup" ] @>

    [<Fact>]
    let ``layer merge has zipPar semantics`` () =
        let left = Layer.succeed 1
        let right = Layer.succeed 2

        let result =
            Flow.env<int * int, string>
            |> Layer.provide (Layer.merge left right)
            |> Flow.runSync ()

        test <@ result = Exit.Success (1, 2) @>

    [<Fact>]
    let ``layer zipPar finalizes successful branch when sibling provisioning fails`` () =
        let calls = ResizeArray<string>()

        let successful =
            Layer.fromFlow (
                flow {
                    do! Flow.scopeFinalizer (Flow.delay (fun () -> calls.Add "success-cleanup"; Flow.succeed ()))
                    return "ok"
                }
            )

        let failed =
            Layer.fromFlow (Flow.fail "failed")

        let result =
            Flow.env<string * string, string>
            |> Layer.provide (Layer.zipPar successful failed)
            |> Flow.runSync ()

        test <@ result = Exit.Failure (Cause.Fail "failed") @>
        test <@ List.ofSeq calls = [ "success-cleanup" ] @>

    [<Fact>]
    let ``layer zipPar accumulates both branch failures`` () =
        let left = Layer.fromFlow (Flow.fail "left")
        let right = Layer.fromFlow (Flow.fail "right")

        let result =
            Flow.env<string * string, string>
            |> Layer.provide (Layer.zipPar left right)
            |> Flow.runSync ()

        test <@ result = Exit.Failure (Cause.Both(Cause.Fail "left", Cause.Fail "right")) @>

    [<Fact>]
    let ``layer map2 map3 and mapError compose provisioning`` () =
        let mappedError =
            Layer.fromFlow (Flow.fail "missing")
            |> Layer.mapError (fun error -> $"layer:{error}")

        let map2Result =
            Layer.map2 (+) (Layer.succeed 1) (Layer.succeed 2)
            |> fun layer ->
                Flow.env<int, string>
                |> Layer.provide layer
                |> Flow.runSync ()

        let map3Result =
            Layer.map3
                (fun a b c -> { A = a; B = b; C = c })
                (Layer.succeed 1)
                (Layer.succeed 2)
                (Layer.succeed 3)
            |> fun layer ->
                Flow.env<LayerCompositionEnv, string>
                |> Layer.provide layer
                |> Flow.runSync ()

        let mappedErrorResult =
            Flow.env<int, string>
            |> Layer.provide mappedError
            |> Flow.runSync ()

        test <@ map2Result = Exit.Success 3 @>
        test <@ map3Result = Exit.Success { A = 1; B = 2; C = 3 } @>
        test <@ mappedErrorResult = Exit.Failure (Cause.Fail "layer:missing") @>

    [<Fact>]
    let ``layer computation expression uses let bang sequentially`` () =
        let calls = ResizeArray<string>()

        let first =
            Layer.fromFlow (Flow.delay (fun () -> calls.Add "first"; Flow.succeed 1))

        let second value =
            Layer.fromFlow (Flow.delay (fun () -> calls.Add $"second:{value}"; Flow.succeed (value + 1)))

        let composed =
            layer {
                let! firstValue = first
                let! secondValue = second firstValue
                return firstValue, secondValue
            }

        let result =
            Flow.env<int * int, string>
            |> Layer.provide composed
            |> Flow.runSync ()

        test <@ result = Exit.Success (1, 2) @>
        test <@ List.ofSeq calls = [ "first"; "second:1" ] @>

    [<Fact>]
    let ``layer computation expression uses and bang for parallel merge`` () =
        let calls = ResizeArray<string>()
        let mutable started = 0
        let bothStarted = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

        let makeLayer name value =
            Layer.fromFlow (
                Flow.fromTask (fun cancellationToken ->
                    task {
                        calls.Add($"{name}-start")

                        if Interlocked.Increment(&started) = 2 then
                            bothStarted.TrySetResult() |> ignore

                        use _ = cancellationToken.Register(fun () -> release.TrySetCanceled(cancellationToken) |> ignore)
                        do! release.Task
                    })
                |> Flow.map (fun () -> value)
            )

        let composed =
            layer {
                let! left = makeLayer "left" 1
                and! right = makeLayer "right" 2

                return left + right
            }

        let workflow =
            Flow.env<int, string>
            |> Layer.provide composed

        let runTask = Task.Run(fun () -> Flow.runSync () workflow)

        try
            test <@ bothStarted.Task.Wait(TimeSpan.FromSeconds 2.0) @>
        finally
            release.TrySetResult() |> ignore

        let result = runTask.GetAwaiter().GetResult()
        let starts = calls |> Seq.filter (fun call -> call.EndsWith("-start")) |> Set.ofSeq

        test <@ result = Exit.Success 3 @>
        test <@ starts = Set.ofList [ "left-start"; "right-start" ] @>

    [<Fact>]
    let ``layer computation expression uses and bang for three-way parallel merge`` () =
        let calls = ResizeArray<string>()
        let mutable started = 0
        let allStarted = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

        let makeLayer name value =
            Layer.fromFlow (
                Flow.fromTask (fun cancellationToken ->
                    task {
                        calls.Add($"{name}-start")

                        if Interlocked.Increment(&started) = 3 then
                            allStarted.TrySetResult() |> ignore

                        use _ = cancellationToken.Register(fun () -> release.TrySetCanceled(cancellationToken) |> ignore)
                        do! release.Task
                    })
                |> Flow.map (fun () -> value)
            )

        let composed =
            layer {
                let! left = makeLayer "left" 1
                and! middle = makeLayer "middle" 2
                and! right = makeLayer "right" 3

                return left + middle + right
            }

        let workflow =
            Flow.env<int, string>
            |> Layer.provide composed

        let runTask = Task.Run(fun () -> Flow.runSync () workflow)

        try
            test <@ allStarted.Task.Wait(TimeSpan.FromSeconds 2.0) @>
        finally
            release.TrySetResult() |> ignore

        let result = runTask.GetAwaiter().GetResult()
        let starts = calls |> Seq.filter (fun call -> call.EndsWith("-start")) |> Set.ofSeq

        test <@ result = Exit.Success 6 @>
        test <@ starts = Set.ofList [ "left-start"; "middle-start"; "right-start" ] @>

    [<Fact>]
    let ``platform services install explicitly at the host boundary`` () =
        let clock = Clock.fromValue (DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero))
        let entries = Log.memory ()
        let workflow = flow {
            let! now = Clock.now
            do! Log.info ("now=" + now.ToString("HH:mm"))
            let! number = Random.nextInt 1 10
            let! id = Guid.newGuid
            return number, id
        }
        let id = System.Guid.Parse "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
        let result = workflow |> Flow.withClock clock |> Flow.withLogSink entries
                              |> Flow.withRandom (Random.fromValue 42) |> Flow.withGuid (Guid.fromValue id)
                              |> Flow.runSync ()
        test <@ result = Exit.Success(42, id) @>
        test <@ entries.Entries |> Seq.map _.Message |> Seq.toList = ["now=10:00"] @>

    [<Fact>]
    let ``missing platform registrations need no service provider`` () =
        let provider = { new IServiceProvider with member _.GetService _ = failwith "must not consult provider" }
        let result = Clock.now |> Flow.withClock (Clock.fromValue DateTimeOffset.UnixEpoch) |> Flow.runSync provider
        test <@ result = Exit.Success DateTimeOffset.UnixEpoch @>

    [<Fact>]
    let ``platform scopes do not replace application environment values`` () =
        let result = Flow.env<string, Never> |> Flow.withRandom (Random.fromValue 7) |> Flow.runSync "application"
        test <@ result = Exit.Success "application" @>
