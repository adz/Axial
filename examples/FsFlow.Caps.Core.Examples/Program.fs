open System
open FsFlow.Caps.Core.Examples

module Runner =
    let run () =
        CoreCapabilitiesExample.run()

[<EntryPoint>]
let main _ =
    Runner.run()
    0
