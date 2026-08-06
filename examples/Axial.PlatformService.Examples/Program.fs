open System
open Axial.PlatformService.Examples

module Runner =
    let run () =
        CoreServicesExample.run()

[<EntryPoint>]
let main _ =
    Runner.run()
    0
