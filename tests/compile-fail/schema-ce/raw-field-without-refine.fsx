#load "references.fsx"

open Axial
open Axial.Schema
open Axial.Schema.Syntax

type Email =
    private
    | Email of string

type Signup = { Email: Email }

schema<Signup> {
    field "email" _.Email {
        withSchema Schema.text
    }

    construct (fun email -> { Email = email })
}
|> ignore
