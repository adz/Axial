namespace FsFlow.Tests

open System.Threading.Tasks
open FsFlow
open FsFlow.Services.Console
open FsFlow.Services.FileSystem
open FsFlow.Services.Http
open FsFlow.Services.Process
open Swensen.Unquote
open Xunit

type UnifiedCaps =
    {
        Console: IConsole
        FS: IFileSystem
        Http: IHttp
        Process: IProcess
    }
    interface IHas<IConsole> with
        member this.Service = this.Console
    interface IHas<IFileSystem> with
        member this.Service = this.FS
    interface IHas<IHttp> with
        member this.Service = this.Http
    interface IHas<IProcess> with
        member this.Service = this.Process

module CapsUnifiedTests =
    [<Fact>]
    let ``Console: read and write`` () =
        let mutable lastMsg = ""
        let caps = { 
            Console = 
                { new IConsole with 
                    member _.ReadLine() = "input"
                    member _.WriteLine(m) = lastMsg <- m }
            FS = Unchecked.defaultof<_>; Http = Unchecked.defaultof<_>; Process = Unchecked.defaultof<_>
        }
        
        let workflow = flow {
            let! input = Console.readLine
            do! Console.writeLine input
            return input
        }
        
        test <@ Flow.runSync caps workflow = Exit.Success "input" @>
        test <@ lastMsg = "input" @>

    [<Fact>]
    let ``FileSystem: exists and read`` () =
        let caps = { 
            FS = 
                { new IFileSystem with 
                    member _.Exists(_) = true
                    member _.ReadAllText(_) = "content"
                    member _.WriteAllText(_, _) = () }
            Console = Unchecked.defaultof<_>; Http = Unchecked.defaultof<_>; Process = Unchecked.defaultof<_>
        }
        
        let workflow = flow {
            let! exists = FileSystem.exists "test.txt"
            let! text = FileSystem.readAllText "test.txt"
            return exists, text
        }
        
        test <@ Flow.runSync caps workflow = Exit.Success (true, "content") @>

    [<Fact>]
    let ``Http: getString`` () =
        let caps = { 
            Http = { new IHttp with member _.GetString(_) = Task.FromResult "html" }
            Console = Unchecked.defaultof<_>; FS = Unchecked.defaultof<_>; Process = Unchecked.defaultof<_>
        }
        
        test <@ Flow.runSync caps (Http.getString "http://example.com") = Exit.Success "html" @>

    [<Fact>]
    let ``Process: execute`` () =
        let caps = { 
            Process = 
                { new IProcess with 
                    member _.Execute(_, _) = Task.FromResult { ExitCode = 0; StdOut = "out"; StdErr = "" } }
            Console = Unchecked.defaultof<_>; FS = Unchecked.defaultof<_>; Http = Unchecked.defaultof<_>
        }
        
        test <@ Flow.runSync caps (Process.execute "echo" "hi") = Exit.Success { ExitCode = 0; StdOut = "out"; StdErr = "" } @>
