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
