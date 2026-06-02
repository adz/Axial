open System
open FsFlow.Services.Core.Examples

module Runner =
    let run () =
        CoreServicesExample.run()

[<EntryPoint>]
let main _ =
    Runner.run()
    0
