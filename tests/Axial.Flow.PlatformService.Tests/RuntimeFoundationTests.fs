namespace Axial.Tests

open System
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.Flow.PlatformService
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
            Layer.fromValueTask (fun (_, scope) _ ->
                scope.AddFinalizer(fun _ ->
                    calls.Add "cleanup"
                    Task.CompletedTask)

                Execution.ofError "startup failed")

        let result =
            Flow.succeed "unreachable"
            |> Flow.provide layer
            |> Flow.runSync ()

        test <@ result = Exit.Failure (Cause.Fail "startup failed") @>
        test <@ List.ofSeq calls = [ "cleanup" ] @>

    [<Fact>]
    let ``provided layer finalizes acquired resources when downstream flow fails`` () =
        let calls = ResizeArray<string>()

        let layer : Layer<unit, string, string> =
            Layer.fromValueTask (fun (_, scope) _ ->
                scope.AddFinalizer(fun _ ->
                    calls.Add "cleanup"
                    Task.CompletedTask)

                Execution.ofValue "resource")

        let result =
            Flow.fail "workflow failed"
            |> Flow.provide layer
            |> Flow.runSync ()

        test <@ result = Exit.Failure (Cause.Fail "workflow failed") @>
        test <@ List.ofSeq calls = [ "cleanup" ] @>

    [<Fact>]
    let ``layer zip provisions sequentially left then right`` () =
        let calls = ResizeArray<string>()

        let left =
            Layer.fromValueTask (fun (_, _) _ ->
                calls.Add "left"
                Execution.ofValue 1)

        let right =
            Layer.fromValueTask (fun (_, _) _ ->
                calls.Add "right"
                Execution.ofValue 2)

        let result =
            Flow.env<int * int, string>
            |> Flow.provide (Layer.zip left right)
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

            Layer.fromValueTask (fun (_, scope) cancellationToken ->
                ValueTask<Exit<int, string>>(
                    task {
                        calls.Add($"{name}-start")

                        if Interlocked.Increment(&started) = 2 then
                            bothStarted.TrySetResult() |> ignore

                        use _ = cancellationToken.Register(fun () -> release.TrySetCanceled(cancellationToken) |> ignore)
                        do! release.Task

                        scope.AddFinalizer(fun _ ->
                            calls.Add($"{name}-cleanup")
                            Task.CompletedTask)

                        return Exit.Success value
                    }))

        let workflow =
            Flow.env<int * int, string>
            |> Flow.provide (Layer.zipPar (makeLayer "left" 1) (makeLayer "right" 2))

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
            |> Flow.provide (Layer.merge left right)
            |> Flow.runSync ()

        test <@ result = Exit.Success (1, 2) @>

    [<Fact>]
    let ``layer zipPar finalizes successful branch when sibling provisioning fails`` () =
        let calls = ResizeArray<string>()

        let successful =
            Layer.fromValueTask (fun (_, scope) _ ->
                scope.AddFinalizer(fun _ ->
                    calls.Add "success-cleanup"
                    Task.CompletedTask)

                Execution.ofValue "ok")

        let failed =
            Layer.fromValueTask (fun (_, _) _ -> Execution.ofError "failed")

        let result =
            Flow.env<string * string, string>
            |> Flow.provide (Layer.zipPar successful failed)
            |> Flow.runSync ()

        test <@ result = Exit.Failure (Cause.Fail "failed") @>
        test <@ List.ofSeq calls = [ "success-cleanup" ] @>

    [<Fact>]
    let ``layer zipPar accumulates both branch failures`` () =
        let left = Layer.fromValueTask (fun (_, _) _ -> Execution.ofError "left")
        let right = Layer.fromValueTask (fun (_, _) _ -> Execution.ofError "right")

        let result =
            Flow.env<string * string, string>
            |> Flow.provide (Layer.zipPar left right)
            |> Flow.runSync ()

        test <@ result = Exit.Failure (Cause.Both(Cause.Fail "left", Cause.Fail "right")) @>

    [<Fact>]
    let ``layer map2 map3 and mapError compose provisioning`` () =
        let mappedError =
            Layer.fromValueTask (fun (_, _) _ -> Execution.ofError "missing")
            |> Layer.mapError (fun error -> $"layer:{error}")

        let map2Result =
            Layer.map2 (+) (Layer.succeed 1) (Layer.succeed 2)
            |> fun layer ->
                Flow.env<int, string>
                |> Flow.provide layer
                |> Flow.runSync ()

        let map3Result =
            Layer.map3
                (fun a b c -> { A = a; B = b; C = c })
                (Layer.succeed 1)
                (Layer.succeed 2)
                (Layer.succeed 3)
            |> fun layer ->
                Flow.env<LayerCompositionEnv, string>
                |> Flow.provide layer
                |> Flow.runSync ()

        let mappedErrorResult =
            Flow.env<int, string>
            |> Flow.provide mappedError
            |> Flow.runSync ()

        test <@ map2Result = Exit.Success 3 @>
        test <@ map3Result = Exit.Success { A = 1; B = 2; C = 3 } @>
        test <@ mappedErrorResult = Exit.Failure (Cause.Fail "layer:missing") @>

    [<Fact>]
    let ``layer computation expression uses let bang sequentially`` () =
        let calls = ResizeArray<string>()

        let first =
            Layer.fromValueTask (fun (_, _) _ ->
                calls.Add "first"
                Execution.ofValue 1)

        let second value =
            Layer.fromValueTask (fun (_, _) _ ->
                calls.Add $"second:{value}"
                Execution.ofValue (value + 1))

        let composed =
            layer {
                let! firstValue = first
                let! secondValue = second firstValue
                return firstValue, secondValue
            }

        let result =
            Flow.env<int * int, string>
            |> Flow.provide composed
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
            Layer.fromValueTask (fun (_, _) cancellationToken ->
                ValueTask<Exit<int, string>>(
                    task {
                        calls.Add($"{name}-start")

                        if Interlocked.Increment(&started) = 2 then
                            bothStarted.TrySetResult() |> ignore

                        use _ = cancellationToken.Register(fun () -> release.TrySetCanceled(cancellationToken) |> ignore)
                        do! release.Task

                        return Exit.Success value
                    }))

        let composed =
            layer {
                let! left = makeLayer "left" 1
                and! right = makeLayer "right" 2

                return left + right
            }

        let workflow =
            Flow.env<int, string>
            |> Flow.provide composed

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
            Layer.fromValueTask (fun (_, _) cancellationToken ->
                ValueTask<Exit<int, string>>(
                    task {
                        calls.Add($"{name}-start")

                        if Interlocked.Increment(&started) = 3 then
                            allStarted.TrySetResult() |> ignore

                        use _ = cancellationToken.Register(fun () -> release.TrySetCanceled(cancellationToken) |> ignore)
                        do! release.Task

                        return Exit.Success value
                    }))

        let composed =
            layer {
                let! left = makeLayer "left" 1
                and! middle = makeLayer "middle" 2
                and! right = makeLayer "right" 3

                return left + middle + right
            }

        let workflow =
            Flow.env<int, string>
            |> Flow.provide composed

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
    let ``base runtime layer provisions explicit services from IServiceProvider`` () =
        let clock = Clock.fromValue (DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero))
        let logMessages = ResizeArray<LogLevel * string>()
        let logger =
            Log.fromSink (fun level message -> logMessages.Add(level, message))
        let random = Random.fromValue 42
        let guid = Guid.fromValue (Guid.Parse "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
        let envVars = EnvironmentVariables.fromPairs [ "AXIAL_TEST", "value" ]

        let provider =
            { new IServiceProvider with
                member _.GetService(serviceType) =
                    if serviceType = typeof<IClock> then clock :> obj
                    elif serviceType = typeof<ILog> then logger :> obj
                    elif serviceType = typeof<IRandom> then random :> obj
                    elif serviceType = typeof<IGuid> then guid :> obj
                    elif serviceType = typeof<IEnvironmentVariables> then envVars :> obj
                    else null }

        let workflow : Flow<BaseRuntime, BaseRuntimeError, string> =
            flow {
                let! now = Clock.now<BaseRuntime, BaseRuntimeError>
                let formattedNow = now.ToString("HH:mm")
                do! Log.info<BaseRuntime, BaseRuntimeError> $"now={formattedNow}"
                let! next = Random.nextInt<BaseRuntime, BaseRuntimeError> 1 10
                let! id = Guid.newGuid<BaseRuntime, BaseRuntimeError>
                let! value = EnvironmentVariables.tryGet<BaseRuntime, BaseRuntimeError> "AXIAL_TEST"
                let envValue = defaultArg value "<missing>"
                return $"{next}:{id}:{envValue}"
            }

        let result =
            workflow
            |> Flow.provide BaseRuntime.fromServiceProvider
            |> Flow.runSync provider

        test <@ result = Exit.Success "42:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa:value" @>
        test <@ List.ofSeq logMessages = [ LogLevel.Information, "now=10:00" ] @>

    [<Fact>]
    let ``base runtime layer reports missing provider services as typed failures`` () =
        let provider =
            { new IServiceProvider with
                member _.GetService(serviceType) =
                    if serviceType = typeof<IClock> then Clock.live :> obj else null }

        let result =
            Flow.env<BaseRuntime, BaseRuntimeError>
            |> Flow.provide BaseRuntime.fromServiceProvider
            |> Flow.runSync provider

        test <@ result = Exit.Failure (Cause.Fail (BaseRuntimeError.MissingService "ILog")) @>

    [<Fact>]
    let ``base runtime remains a plain record of explicit services`` () =
        let runtime =
            {
                Clock = Clock.fromValue (DateTimeOffset(2026, 5, 15, 11, 0, 0, TimeSpan.Zero))
                Log = Log.live
                Random = Random.fromValue 7
                Guid = Guid.fromValue (Guid.Parse "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
                EnvironmentVariables = EnvironmentVariables.fromPairs [ "AXIAL_RUNTIME", "ok" ]
            }

        test <@ runtime.Clock.UtcNow() = DateTimeOffset(2026, 5, 15, 11, 0, 0, TimeSpan.Zero) @>
        test <@ runtime.Random.Next() = 7 @>
        test <@ runtime.Random.NextMax 10 = 7 @>
        test <@ runtime.Random.NextInt 0 10 = 7 @>
        test <@ runtime.Guid.NewGuid() = Guid.Parse "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb" @>
        test <@ runtime.EnvironmentVariables.TryGet "AXIAL_RUNTIME" = Some "ok" @>
