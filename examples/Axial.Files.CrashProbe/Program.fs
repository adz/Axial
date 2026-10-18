open System
open System.Text
open Axial
open Axial.FileSystem
open Axial.Files

let env = { new IHasFileSystem with member _.FileSystem = FileSystem.live }
let run operation =
    match Flow.run env operation with
    | Exit.Success value -> value
    | _ -> failwith "Probe operation failed"

[<EntryPoint>]
let main args =
    match args with
    | [| "atomic"; path |] ->
        let mutable i = 0
        printfn "ready"
        while true do
            let length = 1000 + int ((int64 i * 7919L) % 50000L)
            let payload = Encoding.ASCII.GetBytes(string length + "\n" + String(char (int 'a' + i % 26), length))
            Files.writeAtomic path payload |> run
            i <- i + 1
        0
    | [| "log"; path |] ->
        let operation = flow {
            let! log = AppendLog.openLog path 256L |> Flow.scopeResource
            let! existing = AppendLog.readAll log |> FlowStream.runCollect
            let mutable next = if existing.IsEmpty then 0 else (existing |> List.last |> Encoding.ASCII.GetString |> int) + 1
            printfn "ready"
            while true do
                do! AppendLog.append (Encoding.ASCII.GetBytes(string next)) log
                next <- next + 1
        }
        run operation
        0
    | _ -> invalidArg (nameof args) "Expected atomic|log <path>"
