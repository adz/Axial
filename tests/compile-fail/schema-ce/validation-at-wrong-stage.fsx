#load "references.fsx"

open Axial
open Axial.Refined
open Axial.Schema
open Axial.Schema.Syntax

type Email =
    private
    | Email of string

module Email =
    let create raw = Ok(Email raw)
    let value (Email value) = value
    let refinement = Refinement.define create value

type Email with
    static member Refinement(_: string, _: Email) = Email.refinement

type Signup = { Email: Email }

let validateText (value: string) =
    if value.Length > 3 then
        Ok()
    else
        Error(SchemaError.Custom("short", Some "Too short."))

schema<Signup> {
    field "email" _.Email {
        withSchema Schema.text
        refine
        validate validateText
    }

    construct (fun email -> { Email = email })
}
|> ignore
