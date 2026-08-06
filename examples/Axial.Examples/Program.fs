open System

module Runner =
    let run () =
        SupervisionExample.run()

[<EntryPoint>]
let main _ =
    Runner.run()
    0
