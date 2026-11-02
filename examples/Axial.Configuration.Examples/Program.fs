open System
open Axial.Configuration.Examples

module Runner =
    let run () =
        CoreServicesExample.run()

[<EntryPoint>]
let main _ =
    Runner.run()
    0
