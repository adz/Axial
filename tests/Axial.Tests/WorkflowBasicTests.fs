namespace Axial.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.Result
open Axial.Validation
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
        test <@ workflow.ToValueTask(21, cancellationToken = CancellationToken.None).AsTask().GetAwaiter().GetResult() = Exit.Success 42 @>

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
            Flow.read (fun env -> env + 1)
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
    let ``Runnable example docs are generated from executable example projects`` () =
        let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))
        let docsExamplesPath = Path.Combine(repoRoot, "docs", "patterns", "examples", "_index.md")
        let generatorPath = Path.Combine(repoRoot, "scripts", "generate-example-docs.sh")
        let generatedPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md")

        try
            let exitCode, output =
                runBashScript generatorPath [ "DOCS_EXAMPLES_OUTPUT", generatedPath ]

            if exitCode <> 0 then
                failwithf "generate-example-docs.sh failed with exit code %d:%s%s" exitCode Environment.NewLine output

            test <@ File.ReadAllText generatedPath = File.ReadAllText docsExamplesPath @>
        finally
            if File.Exists generatedPath then
                File.Delete generatedPath

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

        use cts = new CancellationTokenSource()

        let first = Flow.runSyncWithToken () cts.Token workflow
        let second = Flow.runSyncWithToken () cts.Token workflow

        test <@ first = Exit.Success 2 @>
        test <@ second = Exit.Success 4 @>
        test <@ List.ofSeq observedCancellation = [ true; true ] @>

    [<Fact>]
    let ``flow binds ColdTask Result as a plain value`` () =
        let workflow : Flow<unit, string, Result<int, string>> =
            flow {
                return! ColdTask(fun _ -> Task.FromResult(Ok 42))
            }

        test <@ Flow.runSync () workflow = Exit.Success(Ok 42) @>

    [<Fact>]
    let ``shared combinators preserve environment and error semantics`` () =
        let baseWorkflow : Flow<int, int, int> =
            Flow.read (fun env -> env + 1)
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
                let! client = Flow.read _.DeviceClient
                let! value = Flow.read _.Value
                return $"{client.Name}:{value}"
            }

        let composed =
            workflow
            |> Flow.provide appLayer

        let composedResult =
            composed
            |> Flow.runSync ()

        let provider = RecordingServiceProvider(typeof<IDeviceClient>, app.DeviceClient :> obj) :> IServiceProvider

        let providerResult =
            Service<IDeviceClient>.resolve()
            |> Flow.runSync provider

        let missingProviderResult =
            Service<IDeviceClient>.resolve()
            |> Flow.runSync (RecordingServiceProvider(typeof<string>, "nope") :> IServiceProvider)

        let serviceLookup : Flow<AppDependencies, string, IDeviceClient> =
            Service<IDeviceClient>.get()

        let serviceLookupResult =
            serviceLookup
            |> Flow.runSync app

        let flowLayerWorkflow : Flow<AppDependencies, string, string> =
            flow {
                let! client = Flow.read _.DeviceClient
                let! value = Flow.read _.Value
                return $"{client.Name}:{value}"
            }

        let flowLayerResult =
            flowLayerWorkflow
            |> Flow.provide (Layer.succeed app)
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
    let ``flow builder overloads stay aligned with the Fable 5 mapping`` () =
        let publicMethods = publicInstanceMethodNames typeof<FlowBuilder>
        let argumentTypeNames = flowBuilderBindAndReturnFromArgumentNames ()

        test <@ publicMethods |> Array.contains "Bind" @>
        test <@ publicMethods |> Array.contains "ReturnFrom" @>
        test <@ publicMethods |> Array.exists (fun name -> name.StartsWith("Yield")) |> not @>
        test <@ publicMethods |> Array.contains "Run" @>
        test <@ argumentTypeNames = [| "BindError`3"; "ColdTask`1"; "FSharpAsync`1"; "FSharpFunc`2"; "FSharpOption`1"; "FSharpResult`2"; "FSharpValueOption`1"; "Flow`3"; "Task"; "Task`1"; "ValueTask"; "ValueTask`1" |] @>

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

        test <@ typeof<FlowBuilder>.Namespace = "Axial.Flow" @>
        test <@ result = Exit.Success 42 @>
