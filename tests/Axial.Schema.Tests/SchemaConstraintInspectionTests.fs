namespace Axial.Tests

open Axial.Constraint
open Axial.Schema
open Axial.Schema.Syntax
open Axial.Constraint.ConstraintDSL
open Swensen.Unquote
open Xunit

/// <summary>
/// Proves that the rules attached to a schema can be read straight from a <c>Schema&lt;'model&gt;</c> without
/// constructing a trusted model and without running any check. The read model is the unified
/// <see cref="T:Axial.Constraint.ConstraintDescription" />: Schema does not publish a second inspection vocabulary,
/// so one named constraint is the same value whether it is checked directly, used in a refinement, or attached
/// here.
/// </summary>
module ConstraintInspectionTests =
    // `test` is exported by the constraint DSL, so it shadows Unquote's assertion in this file.
    let inline private assertThat (assertion: Quotations.Expr<bool>) = test assertion

    type private Signup = { Email: string; Age: int }

    type private Address =
        { Street: string
          City: string
          PostalCode: string }

    let private atomsOf (description: SchemaDescription) =
        description.Constraints |> List.collect ConstraintDescription.atoms

    [<Fact>]
    let ``the field DSL attaches singular and plural constraints in declaration order`` () =
        let schema =
            schema<Signup> {
                field "email" _.Email {
                    withSchema Schema.text
                    mustSupply
                    constraints [ present; email; minLength 3; maxLength 254 ]
                    constrain trimmed
                }
                field "age" _.Age {
                    constrain (atLeast 13)
                    constraints [ atMost 120; notEqualTo 99 ]
                }
                construct (fun email age -> { Email = email; Age = age })
            }

        let fields = (Inspect.model schema).Fields

        assertThat <@
            atomsOf fields[0].Schema =
                [ PresenceAtom Present
                  FormatAtom Email
                  CardinalityAtom(Cardinality.Minimum 3)
                  CardinalityAtom(Cardinality.Maximum 254)
                  FormatAtom Trimmed ]
        @>

        assertThat <@
            atomsOf fields[1].Schema =
                [ RelationAtom(Compared(AtLeast, ConstraintValue.Integer 13L))
                  RelationAtom(Compared(AtMost, ConstraintValue.Integer 120L))
                  RelationAtom(Compared(NotEqual, ConstraintValue.Integer 99L)) ]
        @>

    [<Fact>]
    let ``supply is inspectable separately from the value constraints`` () =
        let schema =
            schema<Signup> {
                field "email" _.Email {
                    withSchema Schema.text
                    mustSupply
                    constrain present
                }
                field "age" _.Age
                construct (fun email age -> { Email = email; Age = age })
            }

        let fields = (Inspect.model schema).Fields

        assertThat <@ fields[0].Schema.Supply = Some Supply.Supplied @>
        assertThat <@ atomsOf fields[0].Schema = [ PresenceAtom Present ] @>
        assertThat <@ fields[1].Schema.Supply = None @>

    [<Fact>]
    let ``one named constraint is the same value in direct use and in a schema`` () =
        let contact: Constraint<string> = Constraint.all [ present; email; maxLength 254 ]
        let schema = Schema.text |> Schema.constrain contact

        assertThat <@ Schema.constraints schema = [ Constraint.inspect contact ] @>
        assertThat <@ Constraint.test contact "ada@example.com" @>

    [<Fact>]
    let ``constraints attached at every layer stay in authoring order`` () =
        let emailValue = Schema.text |> Schema.constrainAll [ present; email; maxLength 254 ]

        let schema =
            schema<Signup> {
                field "email" _.Email { withSchema (emailValue |> Schema.constrain trimmed) }
                field "age" _.Age { withSchema (Schema.int |> Schema.constrain (Constraint.between 13 120)) }
                construct (fun email age -> { Email = email; Age = age })
            }

        let fields = (Inspect.model schema).Fields

        assertThat <@ fields[0].Constraints = [] @>

        assertThat <@
            atomsOf fields[0].Schema =
                [ PresenceAtom Present; FormatAtom Email; CardinalityAtom(Cardinality.Maximum 254); FormatAtom Trimmed ]
        @>

        assertThat <@
            atomsOf fields[1].Schema =
                [ RelationAtom(Within(ConstraintValue.Integer 13L, ConstraintValue.Integer 120L)) ]
        @>

    [<Fact>]
    let ``per-field metadata is inspectable without constructing a model`` () =
        let schema =
            schema<Address> {
                field "street" _.Street { withSchema (Schema.text |> Schema.constrain present) }
                field "city" _.City { withSchema (Schema.text |> Schema.constrain (lengthBetween 1 100)) }
                field "postalCode" _.PostalCode {
                    withSchema (Schema.text |> Schema.constrainAll [ present; pattern "^[0-9]{5}$" ])
                }
                construct (fun street city postalCode ->
                    { Street = street
                      City = city
                      PostalCode = postalCode })
            }

        let byField =
            (Inspect.model schema).Fields
            |> List.map (fun field -> field.Name, atomsOf field.Schema)

        assertThat <@
            byField =
                [ "street", [ PresenceAtom Present ]
                  "city", [ CardinalityAtom(Cardinality.Between(1, 100)) ]
                  // `constrainAll` composes into one conjunction, so its atoms arrive together and in order.
                  "postalCode", [ PresenceAtom Present; FormatAtom(Pattern "^[0-9]{5}$") ] ]
        @>

    [<Fact>]
    let ``an opaque constraint is visible as opaque without erasing its portable siblings`` () =
        let schema =
            Schema.text
            |> Schema.constrainAll [ present; Constraint.custom "must be an internal address" (fun value -> value.EndsWith "@example.com") ]

        match Schema.constraints schema with
        | [ description ] ->
            match description.Expression with
            | ConstraintExpression.All [ first; second ] ->
                assertThat <@ first.Expression = ConstraintExpression.Atom(PresenceAtom Present) @>

                assertThat <@
                    second.Expression =
                        ConstraintExpression.Opaque(OpaqueConstraint.CustomPredicate "must be an internal address")
                @>
            | other -> failwithf "Expected a conjunction, but was %A." other
        | other -> failwithf "Expected one attached constraint, but was %A." other

    [<Fact>]
    let ``documentary prose reaches inspection without changing the logical form`` () =
        let described =
            Schema.int
            |> Schema.constrain (Constraint.between 0 10 |> Constraint.describe "Retries before the call is abandoned.")

        match Schema.constraints described with
        | [ description ] ->
            assertThat <@ description.Description = Some "Retries before the call is abandoned." @>

            assertThat <@
                description.Expression =
                    ConstraintExpression.Atom(RelationAtom(Within(ConstraintValue.Integer 0L, ConstraintValue.Integer 10L)))
            @>
        | other -> failwithf "Expected one attached constraint, but was %A." other
