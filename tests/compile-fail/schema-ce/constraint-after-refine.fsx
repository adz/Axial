#load "references.fsx"

open Axial
open Axial.Refined
open Axial.Constraint.ConstraintDSL
open Axial.Schema
open Axial.Schema.Syntax

type Email =
    private
    | Email of string

module Email =
    let value (Email value) = value
    let refinement = Refinement.define (Axial.Constraint.Constraint.pattern ".+@.+") Email value

type Email with
    static member Refinement(_: string, _: Email) = Email.refinement

type Signup = { Email: Email }

schema<Signup> {
    field "email" _.Email {
        withSchema Schema.text
        refine
        constrain (minLength 3)
    }

    construct (fun email -> { Email = email })
}
|> ignore
