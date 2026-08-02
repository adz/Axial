open Axial.Constraint
open Axial.Constraint.ConstraintDSL

/// `typeof` inside an inline function is the mechanism operand projection relies on; native AOT trims
/// aggressively, so prove the Guid and TimeSpan atoms survive publication rather than assuming they do.
let private probeOperandProjection () =
    let identifier = System.Guid.Parse "4f489f3b-cd3c-4f53-b99b-fca552f8994d"
    let span = System.TimeSpan.FromMinutes 1.0

    let describes expected (constraint': Constraint<'value>) =
        (Constraint.inspect constraint').Expression = ConstraintExpression.Atom expected

    if not (describes (RelationAtom(Compared(Equal, ConstraintValue.Guid identifier))) (Constraint.equalTo identifier)) then
        failwith "A Guid operand lost its semantic sort under native AOT."

    if not (describes (RelationAtom(Compared(AtLeast, ConstraintValue.TimeSpan span))) (Constraint.atLeast span)) then
        failwith "A TimeSpan operand lost its semantic sort under native AOT."

[<EntryPoint>]
let main _ =
    probeOperandProjection ()

    let name: Constraint<string> = Constraint.all [ present; minLength 3 ]

    "Ada"
    |> guard name
    |> orError "invalid name"
    |> function
        | Ok _ -> 0
        | other -> failwithf "Unexpected constraint probe result: %A" other
