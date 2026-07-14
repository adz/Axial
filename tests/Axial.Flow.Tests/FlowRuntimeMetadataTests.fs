namespace Axial.Tests

open Axial.Flow
open Axial.Tests.TestSupport
open Swensen.Unquote
open Xunit

module FlowRuntimeMetadataTests =
    [<Fact>]
    let ``Flow annotations and trace id are visible inside the annotated flow`` () =
        let workflow =
            Flow.Runtime.annotations
            |> Flow.bind (fun annotations ->
                Flow.Runtime.traceId
                |> Flow.map (fun traceId -> annotations["deviceId"], traceId))
            |> Flow.annotate "deviceId" "device-1"
            |> Flow.traceId "trace-1"

        let result = Flow.runSync () workflow

        test <@ result = Exit.Success ("device-1", Some "trace-1") @>

    [<Fact>]
    let ``Flow annotations restore the outer runtime context after nested flow`` () =
        let inner =
            Flow.Runtime.traceId
            |> Flow.traceId "inner"

        let outer =
            Flow.traceId "outer" (
                flow {
                    let! before = Flow.Runtime.traceId
                    let! during = inner
                    let! after = Flow.Runtime.traceId
                    return before, during, after
                })

        let result = Flow.runSync () outer

        test <@ result = Exit.Success (Some "outer", Some "inner", Some "outer") @>

    [<Fact>]
    let ``Flow annotations override duplicate keys only inside nested flow`` () =
        let inner =
            Flow.Runtime.annotations
            |> Flow.map (Map.find "scope")
            |> Flow.annotate "scope" "inner"

        let outer =
            Flow.annotate "scope" "outer" (
                flow {
                    let! before = Flow.Runtime.annotations |> Flow.map (Map.find "scope")
                    let! during = inner
                    let! after = Flow.Runtime.annotations |> Flow.map (Map.find "scope")
                    return before, during, after
                })

        let result = Flow.runSync () outer

        test <@ result = Exit.Success ("outer", "inner", "outer") @>

    [<Fact>]
    let ``Composed annotation sinks all receive annotations`` () =
        let outer = ResizeArray<string * string>()
        let inner = ResizeArray<string * string>()

        let workflow =
            flow {
                do! Flow.annotate "step" "one" (Flow.succeed ())
                return 42
            }
            |> Flow.addAnnotationSink (fun name value -> inner.Add(name, value))
            |> Flow.addAnnotationSink (fun name value -> outer.Add(name, value))

        let result = Flow.runSync () workflow

        test <@ result = Exit.Success 42 @>
        test <@ List.ofSeq outer = [ "step", "one" ] @>
        test <@ List.ofSeq inner = [ "step", "one" ] @>

    [<Fact>]
    let ``A throwing annotation sink cannot fail the workflow or starve other sinks`` () =
        let surviving = ResizeArray<string>()

        let workflow =
            flow {
                do! Flow.annotate "step" "one" (Flow.succeed ())
                return "done"
            }
            |> Flow.addAnnotationSink (fun _ _ -> failwith "sink bug")
            |> Flow.addAnnotationSink (fun name _ -> surviving.Add name)

        let result = Flow.runSync () workflow

        test <@ result = Exit.Success "done" @>
        test <@ List.ofSeq surviving = [ "step" ] @>

    [<Fact>]
    let ``tracedError wraps failure causes in Cause.Traced and leaves successes untouched`` () =
        let failing =
            (Flow.fail "domain error" : Flow<unit, string, int>)
            |> Flow.tracedError "billing.load-user"

        let succeeding =
            (Flow.succeed 42 : Flow<unit, string, int>)
            |> Flow.tracedError "billing.load-user"

        let failResult = Flow.runSync () failing
        let okResult = Flow.runSync () succeeding

        test <@ failResult = Exit.Failure(Cause.Traced(Cause.Fail "domain error", "billing.load-user")) @>
        test <@ okResult = Exit.Success 42 @>

    [<Fact>]
    let ``tracedError traces defects and renders through prettyPrint`` () =
        let result =
            (Flow.die (System.InvalidOperationException "boom") : Flow<unit, string, int>)
            |> Flow.tracedError "outer-boundary"
            |> Flow.runSync ()

        match result with
        | Exit.Failure(Cause.Traced(Cause.Die error, trace)) ->
            test <@ error.Message = "boom" @>
            test <@ trace = "outer-boundary" @>
            test <@ (Cause.prettyPrint id (Cause.Traced(Cause.Die error, trace))).Contains "Traced(outer-boundary)" @>
        | other -> failwithf "Expected traced defect, got %A" other

    [<Fact>]
    let ``Annotations propagate into forked fibers and their annotations reach the outer sink`` () =
        let sunk = ResizeArray<string * string>()

        let workflow =
            flow {
                let! fiber =
                    Flow.fork (
                        flow {
                            let! inherited = Flow.Runtime.annotations |> Flow.map (Map.tryFind "request")
                            do! Flow.annotate "child" "child-value" (Flow.succeed ())
                            return inherited
                        })

                return! Flow.join fiber
            }
            |> Flow.annotate "request" "req-1"
            |> Flow.addAnnotationSink (fun name value -> lock sunk (fun () -> sunk.Add(name, value)))

        let result = Flow.runSync () workflow

        test <@ result = Exit.Success (Some "req-1") @>
        test <@ lock sunk (fun () -> List.ofSeq sunk) |> List.contains ("child", "child-value") @>

    [<Fact>]
    let ``Annotations from every retry and supervise attempt reach the sink`` () =
        let sunk = ResizeArray<string * string>()
        let sink name value = lock sunk (fun () -> sunk.Add(name, value))

        let retryAttempts = ref 0

        let retried =
            Flow.delay(fun () ->
                retryAttempts.Value <- retryAttempts.Value + 1

                Flow.annotate "retry-attempt" (string retryAttempts.Value) (
                    if retryAttempts.Value < 3 then Flow.fail "transient" else Flow.succeed ()))
            |> Flow.Runtime.retry (RetryPolicy.noDelay 5)
            |> Flow.addAnnotationSink sink

        let superviseAttempts = ref 0

        let supervised =
            Flow.delay(fun () ->
                superviseAttempts.Value <- superviseAttempts.Value + 1

                Flow.annotate "supervise-attempt" (string superviseAttempts.Value) (
                    if superviseAttempts.Value < 3 then
                        Flow.die (System.InvalidOperationException "crash")
                    else
                        Flow.succeed ()))
            |> Flow.Runtime.supervise (SupervisePolicy.noDelay 5)
            |> Flow.addAnnotationSink sink

        test <@ Flow.runSync () retried = Exit.Success () @>
        test <@ Flow.runSync () supervised = Exit.Success () @>

        let entries = lock sunk (fun () -> List.ofSeq sunk)
        test <@ entries |> List.filter (fun (name, _) -> name = "retry-attempt") |> List.map snd = [ "1"; "2"; "3" ] @>
        test <@ entries |> List.filter (fun (name, _) -> name = "supervise-attempt") |> List.map snd = [ "1"; "2"; "3" ] @>

    [<Fact>]
    let ``Annotations inside provided layers and resource-using flows reach the outer sink`` () =
        let sunk = ResizeArray<string * string>()
        let released = ref false

        let layer = Layer.succeed "service"

        let workflow =
            flow {
                let! resource =
                    Flow.acquireRelease
                        (Flow.succeed "resource")
                        (fun _ _ ->
                            released.Value <- true
                            System.Threading.Tasks.Task.CompletedTask)

                do! Flow.annotate "resource" resource (Flow.succeed ())
                let! service = Flow.env<string, string>
                do! Flow.annotate "layered" service (Flow.succeed ())
                return service
            }
            |> Flow.provide layer
            |> Flow.addAnnotationSink (fun name value -> lock sunk (fun () -> sunk.Add(name, value)))

        let result = Flow.runSync () workflow
        let entries = lock sunk (fun () -> List.ofSeq sunk)

        test <@ result = Exit.Success "service" @>
        test <@ released.Value @>
        test <@ entries |> List.contains ("resource", "resource") @>
        test <@ entries |> List.contains ("layered", "service") @>
