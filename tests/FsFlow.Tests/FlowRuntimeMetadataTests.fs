namespace FsFlow.Tests

open FsFlow
open FsFlow.Tests.TestSupport
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
