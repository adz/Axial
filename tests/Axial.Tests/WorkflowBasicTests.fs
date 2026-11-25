namespace Axial.Tests

open System
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Tests.TestSupport
open Swensen.Unquote
open Xunit

module WorkflowBasicTests =
    [<Fact>]
    let ``Flow is sync result only`` () =
        let workflow : Flow<int, string, int> =
            Flow.env
            |> Flow.bind (fun value -> Flow.succeed(value * 2))

        test <@ Flow.runSync 21 workflow = Exit.Success 42 @>

    [<Fact>]
    let ``Flow execution members use explicit cancellation token`` () =
        let workflow : Flow<int, string, int> =
            Flow.env
            |> Flow.map (fun value -> value * 2)

        test <@ Flow.runSync 21 workflow = Exit.Success 42 @>
        test <@ workflow.RunSynchronously(21, cancellationToken = CancellationToken.None) = Exit.Success 42 @>
        test <@ workflow.StartAsValueTask(21, cancellationToken = CancellationToken.None).AsTask().GetAwaiter().GetResult() = Exit.Success 42 @>

    [<Fact>]
    let ``Flow delay reruns from scratch`` () =
        let runs = ref 0

        let workflow : Flow<unit, string, int> =
            Flow.delay(fun () ->
                runs.Value <- runs.Value + 1
                Flow.succeed runs.Value)

        test <@ Flow.runSync () workflow = Exit.Success 1 @>
        test <@ Flow.runSync () workflow = Exit.Success 2 @>

    [<Fact>]
    let ``shared combinators preserve sync and async environment semantics`` () =
        let syncBase : Flow<int, int, int> =
            Flow.envWith (fun env -> env + 1)
            |> Flow.map ((*) 2)
            |> Flow.bind (fun value -> Flow.succeed(value + 3))
            |> Flow.mapError String.length

        let syncWorkflow : Flow<string, int, int> =
            Flow.localEnv String.length syncBase

        let syncResult = Flow.runSync "flowkit" syncWorkflow

        test <@ syncResult = Exit.Success 19 @>

    [<Fact>]
    let ``flow family exposes normalized constructors operators and fallback helpers`` () =
        let syncOk = Flow.ok 41
        let syncAlias = Flow.succeed 41
        let syncError = Flow.error "missing"
        let syncErrorAlias = Flow.fail "missing"

        let syncMapped =
            Flow.(<!>) ((+) 1) syncOk
            |> Flow.runSync ()

        let syncApplied =
            Flow.(<*>) (Flow.ok ((+) 1)) syncOk
            |> Flow.runSync ()

        let syncMapped3 =
            Flow.map3 (fun left middle right -> left + middle + right) (Flow.ok 1) (Flow.ok 2) (Flow.ok 3)
            |> Flow.runSync ()

        let syncIgnored =
            Flow.ignore syncOk
            |> Flow.runSync ()

        let syncBound =
            Flow.(>>=) syncOk (fun value -> Flow.ok (value + 1))
            |> Flow.runSync ()

        let syncRecovered =
            Flow.orElseWith (fun (error: string) -> Flow.ok error.Length) syncError
            |> Flow.runSync ()

        test <@ Flow.runSync () syncOk = Flow.runSync () syncAlias @>
        test <@ Flow.runSync () syncError = Flow.runSync () syncErrorAlias @>
        test <@ syncMapped = Exit.Success 42 @>
        test <@ syncApplied = Exit.Success 42 @>
        test <@ syncMapped3 = Exit.Success 6 @>
        test <@ syncIgnored = Exit.Success () @>
        test <@ syncBound = Exit.Success 42 @>
        test <@ syncRecovered = Exit.Success 7 @>

    [<Fact>]
    let ``Flow delay reruns from scratch even for async work`` () =
        let runs = ref 0

        let workflow : Flow<unit, string, int> =
            Flow.delay(fun () ->
                runs.Value <- runs.Value + 1
                Flow.succeed runs.Value)

        let runOnce () =
            workflow
            |> Flow.runSync ()

        test <@ runOnce () = Exit.Success 1 @>
        test <@ runOnce () = Exit.Success 2 @>

    [<Fact>]
    let ``flow binds ColdTask with runtime cancellation and rerun semantics`` () =
        let runs = ref 0
        let observedCancellation = ResizeArray<bool>()

        let load =
            ColdTask(fun cancellationToken ->
                task {
                    runs.Value <- runs.Value + 1
                    observedCancellation.Add cancellationToken.CanBeCanceled
                    return runs.Value
                })

        let workflow : Flow<unit, string, int> =
            flow {
                let! value = load
                return value * 2
            }

        let returned : Flow<unit, string, int> =
            flow {
                return! load
            }

        use cts = new CancellationTokenSource()

        let first = Flow.runSyncWithToken () cts.Token workflow
        let second = Flow.runSyncWithToken () cts.Token workflow

        test <@ first = Exit.Success 2 @>
        test <@ second = Exit.Success 4 @>
        test <@ Flow.runSyncWithToken () cts.Token returned = Exit.Success 3 @>
        test <@ List.ofSeq observedCancellation = [ true; true; true ] @>

    [<Fact>]
    let ``flow lifts ColdTask Result through let and return`` () =
        let succeeds : Flow<unit, string, int> =
            flow {
                let! value =
                    ColdTask(fun _ -> Task.FromResult(Ok 41 : Result<int, string>))
                    |> Flow.fromColdTaskResult
                return value + 1
            }

        let fails : Flow<unit, string, int> =
            flow {
                return!
                    ColdTask(fun _ -> Task.FromResult(Error "unavailable"))
                    |> Flow.fromColdTaskResult
            }

        test <@ Flow.runSync () succeeds = Exit.Success 42 @>
        test <@ Flow.runSync () fails = Exit.Failure(Cause.Fail "unavailable") @>

    [<Fact>]
    let ``async interop Result constructors lift the typed error channel`` () =
        let taskFlow : Flow<unit, string, int> =
            Flow.fromTaskResult(fun _ -> Task.FromResult(Error "task"))

        let valueTaskFlow : Flow<unit, string, int> =
            Flow.fromValueTaskResult(fun _ -> ValueTask.FromResult(Error "value-task"))

        let asyncFlow : Flow<unit, string, int> =
            Flow.fromAsyncResult(async { return Error "async" })

        let startedTaskFlow : Flow<unit, string, int> =
            Flow.awaitStartedTaskResult(Task.FromResult(Error "started-task"))

        let startedValueTaskFlow : Flow<unit, string, int> =
            Flow.awaitStartedValueTaskResult(ValueTask.FromResult(Error "started-value-task"))

        test <@ Flow.runSync () taskFlow = Exit.Failure(Cause.Fail "task") @>
        test <@ Flow.runSync () valueTaskFlow = Exit.Failure(Cause.Fail "value-task") @>
        test <@ Flow.runSync () asyncFlow = Exit.Failure(Cause.Fail "async") @>
        test <@ Flow.runSync () startedTaskFlow = Exit.Failure(Cause.Fail "started-task") @>
        test <@ Flow.runSync () startedValueTaskFlow = Exit.Failure(Cause.Fail "started-value-task") @>

    [<Fact>]
    let ``shared combinators preserve environment and error semantics`` () =
        let baseWorkflow : Flow<int, int, int> =
            Flow.envWith (fun env -> env + 1)
            |> Flow.map ((*) 2)
            |> Flow.bind (fun value -> Flow.succeed(value + 3))
            |> Flow.mapError String.length

        let workflow : Flow<string, int, int> =
            Flow.localEnv String.length baseWorkflow

        let result =
            workflow
            |> Flow.runSync "flowkit"

        test <@ result = Exit.Success 19 @>

    [<Fact>]
    let ``Flow runtime helpers stay separate from app dependencies`` () =
        let runtime = { RuntimePrefix = "rt:"; Seen = ResizeArray() }

        let app =
            { DeviceClient =
                  { new IDeviceClient with
                      member _.Name = "client" }
              Value = 41 }

        let workflow : Flow<AppDependencies, string, string> =
            flow {
                let! env = Flow.env
                runtime.Seen.Add $"value={env.Value}"
                return $"{runtime.RuntimePrefix}{env.Value}"
            }

        let result =
            Flow.runSync app workflow

        test <@ result = Exit.Success "rt:41" @>
        test <@ List.ofSeq runtime.Seen = [ "value=41" ] @>

    [<Fact>]
    let ``Flow layers and service helpers compose`` () =
        let app =
            { DeviceClient =
                  { new IDeviceClient with
                      member _.Name = "provider-client" }
              Value = 10 }

        let appLayer : Layer<unit, string, AppDependencies> =
            Layer.succeed app

        let workflow : Flow<AppDependencies, string, string> =
            flow {
                let! client = Flow.envWith _.DeviceClient
                let! value = Flow.envWith _.Value
                return $"{client.Name}:{value}"
            }

        let composed =
            workflow
            |> Layer.provide appLayer

        let composedResult =
            composed
            |> Flow.runSync ()

        let provider = RecordingServiceProvider(typeof<IDeviceClient>, app.DeviceClient :> obj) :> IServiceProvider

        let providerResult =
            ServiceProvider.get<IDeviceClient, _, _>()
            |> Flow.runSync provider

        let missingProviderResult =
            ServiceProvider.get<IDeviceClient, _, _>()
            |> Flow.runSync (RecordingServiceProvider(typeof<string>, "nope") :> IServiceProvider)

        let serviceLookup : Flow<AppDependencies, string, IDeviceClient> =
            Flow.envWith _.DeviceClient

        let serviceLookupResult =
            serviceLookup
            |> Flow.runSync app

        let flowLayerWorkflow : Flow<AppDependencies, string, string> =
            flow {
                let! client = Flow.envWith _.DeviceClient
                let! value = Flow.envWith _.Value
                return $"{client.Name}:{value}"
            }

        let flowLayerResult =
            flowLayerWorkflow
            |> Layer.provide (Layer.succeed app)
            |> Flow.runSync ()

        test <@ composedResult = Exit.Success "provider-client:10" @>
        test <@ providerResult = Exit.Success app.DeviceClient @>
        
        match missingProviderResult with
        | Exit.Failure (Cause.Die ex) when ex.Message.Contains "IDeviceClient" -> ()
        | _ -> failwithf "Expected Die failure for missing service, got %A" missingProviderResult

        test <@ serviceLookupResult = Exit.Success app.DeviceClient @>
        test <@ flowLayerResult = Exit.Success "provider-client:10" @>

    [<Fact>]
    let ``Flow traverse and sequence work as expected`` () =
        let values = [ 1; 2; 3 ]
        let workflow = values |> Flow.traverse (fun v -> Flow.succeed (v * 2))
        let result = Flow.runSync () workflow
        test <@ result = Exit.Success [ 2; 4; 6 ] @>

        let flows = [ Flow.succeed 1; Flow.succeed 2 ]
        let sequenceResult = Flow.runSync () (Flow.sequence flows)
        test <@ sequenceResult = Exit.Success [ 1; 2 ] @>

        let failWorkflow = [ 1; 2 ] |> Flow.traverse (fun v -> if v = 1 then Flow.fail "error" else Flow.succeed v)
        test <@ Flow.runSync () failWorkflow = Exit.Failure (Cause.Fail "error") @>

    [<Fact>]
    let ``Flow forEach runs each value in order, stops at the first failure and disposes the sequence`` () =
        let seen = ResizeArray<int>()
        let disposed = ref 0
        let values () =
            seq {
                try
                    for value in 1..5 do
                        yield value
                finally
                    disposed.Value <- disposed.Value + 1
            }
        let record value : Flow<unit, string, unit> =
            Flow.delay (fun () ->
                seen.Add value
                if value = 3 then Flow.fail "three" else Flow.ok ())

        let workflow = values () |> Flow.forEach record
        test <@ seen.Count = 0 @>
        test <@ Flow.runSync () workflow = Exit.Failure(Cause.Fail "three") @>
        test <@ List.ofSeq seen = [ 1; 2; 3 ] @>
        test <@ disposed.Value = 1 @>

        seen.Clear()
        test <@ Flow.runSync () (values () |> Flow.forEach (fun value -> Flow.delay (fun () -> seen.Add value; Flow.ok ()))) = Exit.Success () @>
        test <@ List.ofSeq seen = [ 1..5 ] @>
        test <@ disposed.Value = 2 @>

        let empty : Exit<unit, string> = Seq.empty |> Flow.forEach (fun (_: int) -> Flow.ok ()) |> Flow.runSync ()
        test <@ empty = Exit.Success () @>

    [<Fact>]
    let ``flow builder overloads stay aligned with the Fable 5 mapping`` () =
        let publicMethods = publicInstanceMethodNames typeof<FlowBuilder>
        let argumentTypeNames = flowBuilderBindAndReturnFromArgumentNames ()

        test <@ publicMethods |> Array.contains "Bind" @>
        test <@ publicMethods |> Array.contains "ReturnFrom" @>
        test <@ publicMethods |> Array.exists (fun name -> name.StartsWith("Yield")) |> not @>
        test <@ publicMethods |> Array.contains "Run" @>
        test <@ argumentTypeNames = [| "FSharpFunc`2"; "Flow`3" |] @>

    [<Fact>]
    let ``flow lives in Axial Flow and composes sync flows`` () =
        let workflow : Flow<int, string, int> =
            flow {
                let! env = Flow.env
                let! baseValue = Flow.succeed(env + 1)
                return baseValue * 2
            }

        let result =
            workflow
            |> Flow.runSync 20

        test <@ typeof<FlowBuilder>.Namespace = "Axial" @>
        test <@ result = Exit.Success 42 @>

    [<Fact>]
    let ``zip map2 apply and map3 combine successful values`` () =
        let zipped = Flow.zip (Flow.ok 1) (Flow.ok "two") |> Flow.runSync ()
        let mapped2 = Flow.map2 (+) (Flow.ok 1) (Flow.ok 2) |> Flow.runSync ()
        let applied = Flow.apply (Flow.ok (fun value -> value * 10)) (Flow.ok 4) |> Flow.runSync ()
        let mapped3 = Flow.map3 (fun a b c -> a + b + c) (Flow.ok "a") (Flow.ok "b") (Flow.ok "c") |> Flow.runSync ()

        test <@ zipped = Exit.Success(1, "two") @>
        test <@ mapped2 = Exit.Success 3 @>
        test <@ applied = Exit.Success 40 @>
        test <@ mapped3 = Exit.Success "abc" @>

    [<Fact>]
    let ``zip map2 apply and map3 stop at the first failure and skip the flows after it`` () =
        let ran = Collections.Generic.List<string>()

        let step name (outcome: Result<int, string>) : Flow<unit, string, int> =
            Flow.delay (fun () ->
                ran.Add name
                Flow.fromResult outcome)

        let runRecording (workflow: Flow<unit, string, int>) : Exit<int, string> * string list =
            ran.Clear()
            let exit = workflow |> Flow.runSync ()
            exit, List.ofSeq ran

        let add3 a b c = a + b + c

        let zipLeftFails = Flow.zip (step "left" (Error "left")) (step "right" (Ok 2)) |> Flow.map fst |> runRecording
        let zipRightFails = Flow.zip (step "left" (Ok 1)) (step "right" (Error "right")) |> Flow.map fst |> runRecording
        let map2BothFail = Flow.map2 (+) (step "left" (Error "left")) (step "right" (Error "right")) |> runRecording
        let map2RightFails = Flow.map2 (+) (step "left" (Ok 1)) (step "right" (Error "right")) |> runRecording
        let applyBothFail = Flow.apply (step "fn" (Error "fn") |> Flow.map (+)) (step "value" (Error "value")) |> runRecording
        let applyValueFails = Flow.apply (step "fn" (Ok 1) |> Flow.map (+)) (step "value" (Error "value")) |> runRecording
        let map3MiddleFails = Flow.map3 add3 (step "a" (Ok 1)) (step "b" (Error "b")) (step "c" (Error "c")) |> runRecording
        let map3LastFails = Flow.map3 add3 (step "a" (Ok 1)) (step "b" (Ok 2)) (step "c" (Error "c")) |> runRecording

        test <@ zipLeftFails = (Exit.Failure(Cause.Fail "left"), [ "left" ]) @>
        test <@ zipRightFails = (Exit.Failure(Cause.Fail "right"), [ "left"; "right" ]) @>
        test <@ map2BothFail = (Exit.Failure(Cause.Fail "left"), [ "left" ]) @>
        test <@ map2RightFails = (Exit.Failure(Cause.Fail "right"), [ "left"; "right" ]) @>
        test <@ applyBothFail = (Exit.Failure(Cause.Fail "fn"), [ "fn" ]) @>
        test <@ applyValueFails = (Exit.Failure(Cause.Fail "value"), [ "fn"; "value" ]) @>
        test <@ map3MiddleFails = (Exit.Failure(Cause.Fail "b"), [ "a"; "b" ]) @>
        test <@ map3LastFails = (Exit.Failure(Cause.Fail "c"), [ "a"; "b"; "c" ]) @>

    [<Fact>]
    let ``zip map2 apply and map3 pass a defect through unchanged`` () =
        let defect = InvalidOperationException "broken"
        let broken : Flow<unit, string, int> = Flow.die defect

        test <@ Flow.zip broken (Flow.ok 2) |> Flow.runSync () = Exit.Failure(Cause.Die defect) @>
        test <@ Flow.zip (Flow.ok 1) broken |> Flow.runSync () = Exit.Failure(Cause.Die defect) @>
        test <@ Flow.map2 (+) (Flow.ok 1) broken |> Flow.runSync () = Exit.Failure(Cause.Die defect) @>
        test <@ Flow.apply (Flow.ok id) broken |> Flow.runSync () = Exit.Failure(Cause.Die defect) @>
        test <@ Flow.map3 (fun a b c -> a + b + c) (Flow.ok 1) (Flow.ok 2) broken |> Flow.runSync () = Exit.Failure(Cause.Die defect) @>

    [<Fact>]
    let ``sync runs its computation each time the flow runs, not when it is built`` () =
        let calls = ref 0
        let counted : Flow<unit, string, int> = Flow.sync (fun () -> calls.Value <- calls.Value + 1; calls.Value)
        test <@ calls.Value = 0 @>
        test <@ Flow.runSync () counted = Exit.Success 1 @>
        test <@ Flow.runSync () counted = Exit.Success 2 @>

    [<Fact>]
    let ``envWithFlow runs the flow the environment selects`` () =
        let doubled : Flow<int, string, int> = Flow.envWithFlow (fun environment -> Flow.ok (environment * 2))
        test <@ Flow.runSync 21 doubled = Exit.Success 42 @>

    [<Fact>]
    let ``provide runs a flow with a fixed environment whatever the caller's`` () =
        let provided : Flow<string, string, int> = Flow.env<int, string> |> Flow.provide 5
        test <@ Flow.runSync "outer" provided = Exit.Success 5 @>
