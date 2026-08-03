namespace Axial.Refined.Tests

open Axial.Constraint
open Axial.Refined
open Swensen.Unquote
open Xunit

module RefinementTests =
    type CustomerId = private CustomerId of int with member this.Value = let (CustomerId value) = this in value

    let private customerId = Refinement.define (Constraint.greaterThan 0) CustomerId _.Value

    [<Fact>]
    let ``create checks before construction and returns the violation directly`` () =
        test <@ Refinement.create customerId 2 |> Result.map _.Value = Ok 2 @>

        test <@
            Refinement.create customerId 0 =
                Error(Atomic(Expected(RelationAtom(Compared(GreaterThan, ConstraintValue.Integer 0L)), Some(ConstraintValue.Integer 0L))))
        @>

    [<Fact>]
    let ``successful refinement projects to its original underlying value`` () =
        let refined = Refinement.create customerId 3 |> Result.defaultWith (failwithf "%A")
        test <@ Refinement.underlying customerId refined = 3 @>

    [<Fact>]
    let ``a refinement retains the one constraint it admits by`` () =
        let description = Refinement.constraint' customerId |> Constraint.inspect

        test <@ description.Expression = ConstraintExpression.Atom(RelationAtom(Compared(GreaterThan, ConstraintValue.Integer 0L))) @>

    [<Fact>]
    let ``several rules compose with Constraint.all before the refinement is defined`` () =
        // There is no plural or check-taking constructor: composition happens in the constraint vocabulary.
        let rule: Constraint<string> = Constraint.all [ Constraint.minLength 3; Constraint.pattern "^[a-z]+$" ]
        let refinement = Refinement.define rule id id

        match Refinement.create refinement "1" with
        | Error violation -> test <@ Violation.flatten violation |> List.length = 2 @>
        | Ok _ -> failwith "Expected failures."
