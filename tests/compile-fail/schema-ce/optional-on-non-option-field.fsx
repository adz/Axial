#load "references.fsx"

open Axial
open Axial.Schema
open Axial.Schema.Syntax

// A field can only be optional when its type can hold an absent input. `Name` is `string`, so there is
// nowhere to put an absent value and the constructor could never be applied — `optional` must not compile.
type Contact = { Name: string }

schema<Contact> {
    field "name" _.Name {
        constrain optional
    }

    construct (fun name -> { Name = name })
}
|> ignore
