namespace Prototype

open System

module Program =
    [<EntryPoint>]
    let main _ =
        let initial =
            BookingGenerated.create {
                Start = DateOnly(2026, 1, 1)
                End = DateOnly(2026, 1, 2)
            }

        match initial with
        | Error error ->
            printfn "unexpected create failure: %s" error
            1
        | Ok booking ->
            let valid =
                booking
                |> BookingGenerated.update (fun draft ->
                    { draft with End = DateOnly(2026, 1, 3) })

            let invalid =
                booking
                |> BookingGenerated.update (fun draft ->
                    { draft with End = DateOnly(2025, 12, 31) })

            printfn "valid update: %A" valid
            printfn "invalid update: %A" invalid
            0
