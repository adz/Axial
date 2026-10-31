module Axial.Configuration.Tests.ProcessArgTests

open System
open Xunit
open Swensen.Unquote
open Axial
open Axial.Configuration
open Axial.Console
open Axial.FileSystem
open Axial.PlatformService
open Axial.Process
open Axial.Process.DSL
open Reified.Refinements

type ProcessEnvironment =
    { Process: IProcess
      Clock: IClock
      FileSystem: IFileSystem
      Console: IConsole }
    interface IHasProcess with
        member this.Process = this.Process

let private environment () =
    { Process = Process.live Clock.live FileSystem.live Console.live
      Clock = Clock.live
      FileSystem = FileSystem.live
      Console = Console.live }

[<Fact>]
let ``processArg redacts the plan, transcript and failure text`` () =
    let payload = "super-secret-token"
    let token = Secret.create payload
    let command = cmd $"printf %%s {Config.processArg token}"

    let rendered = Process.render command
    let plan = Process.plan command
    test <@ rendered = "printf \"%s\" ***" @>
    test <@ plan.Commands = [ "printf \"%s\" ***" ] @>
    test <@ not (rendered.Contains payload) @>

    match command |> Process.toFlow |> Flow.run (environment ()) with
    | Exit.Success result ->
        test <@ result.Stages.Head.Command = "printf \"%s\" ***" @>
        test <@ not (result.Stages.Head.Command.Contains payload) @>
    | Exit.Failure cause -> failwithf "Expected success, got %A" cause

    let failing = cmd $"sh -c 'exit 3' {Config.processArg token}"

    match failing |> Process.toFlow |> Flow.run (environment ()) with
    | Exit.Success result -> failwithf "Expected failure, got %A" result
    | Exit.Failure cause ->
        let text = sprintf "%O" cause
        test <@ text.Contains "***" @>
        test <@ not (text.Contains payload) @>
