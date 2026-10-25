#load "preamble.fsx"
open System
open Axial
open Preamble

let program : Flow<unit, string, int> =
    flow {
        let! value = async { return raise (InvalidOperationException "boom") }
        return value
    }
