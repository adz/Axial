#load "references.fsx"

open Axial.Refined

type Email =
    private
    | Email of string

type Email with
    static member Refinement(_: string, _: Email) =
        Refinement.define (Axial.Constraint.Constraint.pattern ".+@.+") Email (fun (Email value) -> value)

    static member Refinement(_: string, _: Email) =
        Refinement.define (Axial.Constraint.Constraint.pattern ".+@.+") Email (fun (Email value) -> value)
