open System
open System.Threading
open System.Threading.Tasks
open FsFlow

type ProbeFailure(message: string) =
    inherit Exception(message)

module Assert =
    let equal<'value when 'value: equality> (expected: 'value) (actual: 'value) =
        if actual <> expected then
            raise (ProbeFailure(sprintf "Expected %A but got %A." expected actual))

let flowProbe () =
    let workflow : Flow<string, string, int> =
        Flow.read String.length

    workflow
    |> Flow.run "Ada"
    |> Assert.equal (Exit.Success 3)

let asyncProbe () =
    let workflow : Flow<int, string, int> =
        flow {
            let! value = async { return 21 }
            return value * 2
        }

    workflow
    |> Flow.run 21
    |> Assert.equal (Exit.Success 42)

let taskProbe () =
    let workflow : Flow<unit, string, int> =
        flow {
            let! value = Task.FromResult 42
            return value
        }

    let result =
        workflow
        |> Flow.run ()

    Assert.equal (Exit.Success 42) result

[<EntryPoint>]
let main _ =
    flowProbe ()
    asyncProbe ()
    taskProbe ()
    0
