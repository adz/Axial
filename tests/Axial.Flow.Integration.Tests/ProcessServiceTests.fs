namespace Axial.Tests

open System.Collections.Concurrent
open System.Threading.Tasks
open Axial.Flow
open Axial.Flow.Process
open Axial.Flow.Process.DSL
open Swensen.Unquote
open Xunit

type ProcessTestEnv =
    { Process: IProcess }
    interface IHas<IProcess> with
        member this.Service = this.Process

module ProcessServiceTests =
    let private env = { Process = Process.live }

    [<Fact>]
    let ``arguments stay tokenized and pipelines connect stdout to stdin`` () =
        let workflow =
            cmd "printf" [ "%s"; "hello world" ]
            |>> cmd "tr" [ "[:lower:]"; "[:upper:]" ]
            |> run

        match Flow.runSync env workflow with
        | Exit.Success result ->
            test <@ result.StdOut = "HELLO WORLD" @>
            test <@ result.StdErr = "" @>
            test <@ result.ExitCodes = [ 0; 0 ] @>
        | failure -> failwithf "Expected success, got %A" failure

    [<Fact>]
    let ``streaming observes stdout and stderr while preserving the complete result`` () =
        let observed = ConcurrentQueue<ProcessOutput>()
        let observe output = task { observed.Enqueue output }

        let workflow =
            Process.command "sh" [ "-c"; "printf out; printf err >&2" ]
            |> Process.pipeline
            |> Process.runStreaming observe

        match Flow.runSync env workflow with
        | Exit.Success result ->
            test <@ result.StdOut = "out" @>
            test <@ result.StdErr = "err" @>
            test <@ observed |> Seq.exists (function ProcessOutput.StdOut "out" -> true | _ -> false) @>
            test <@ observed |> Seq.exists (function ProcessOutput.StdErr(0, "err") -> true | _ -> false) @>
        | failure -> failwithf "Expected success, got %A" failure

    [<Fact>]
    let ``run reports every non-zero stage through the typed error channel`` () =
        let workflow =
            Process.command "sh" [ "-c"; "exit 7" ]
            |>> Process.command "cat" []
            |> Process.run

        match Flow.runSync env workflow with
        | Exit.Failure(Cause.Fail(ProcessError.ExitedNonZero result)) ->
            test <@ result.ExitCodes = [ 7; 0 ] @>
        | result -> failwithf "Expected a typed non-zero exit, got %A" result
