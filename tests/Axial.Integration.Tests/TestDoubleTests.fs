namespace Axial.Tests

open System
open Axial
open Axial.Console
open Axial.Process
open Swensen.Unquote
open Xunit

module TestDoubleTests =
    [<Fact>]
    let ``test console captures output and reads scripted input`` () =
        let console = Console.test ()
        let c = console :> IConsole

        c.WriteLine "hello"
        c.WriteErrorLine "oops"
        console.SetInput "world"

        test <@ console.OutText = "hello" + Environment.NewLine @>
        test <@ console.ErrorText = "oops" + Environment.NewLine @>
        test <@ c.ReadLine() = "world" @>

    [<Fact>]
    let ``test process records specs and returns scripted results`` () =
        let processService = Process.test ()
        let p = processService :> IProcess
        processService.RunHandler <- fun _ -> TestProcess.Result(0, "done")

        let result = Flow.runSync () (p.Execute(Process.commandText "echo hi" |> Process.ofCommand, fun _ -> async.Return()))

        test <@ result = Exit.Success (TestProcess.Result(0, "done")) @>
        test <@ processService.RunSpecs.Count = 1 @>
