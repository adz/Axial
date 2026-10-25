#load "preamble.fsx"
open Axial

type UserSource =
    | UserSource of int
    static member Convert(_: FlowBindable, UserSource value) : Flow<unit, string, int> =
        Flow.succeed value

let adapted : Flow<unit, string, int> = flow { let! value = UserSource 42 in return value }
printfn "user-defined source compiled"
