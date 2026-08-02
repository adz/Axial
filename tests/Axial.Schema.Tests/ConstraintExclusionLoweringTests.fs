namespace Axial.Tests

open Axial.Constraint
open Axial.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Proves that the excluding membership rules reach JSON Schema as enforcement rather than as runtime metadata,
/// and that several of them on one value collapse into the single <c>not</c> keyword a schema node allows.
/// </summary>
/// <remarks>
/// The pair exists so exclusion stays interpreted. <c>Constraint.notWith</c> runs the same predicate but describes
/// as opaque, which costs the keyword entirely — these tests are what stops that regressing.
/// </remarks>
module ConstraintExclusionLoweringTests =
    let private document schema = JsonSchema.generateValue schema

    [<Fact>]
    let ``noneOf lowers to a refused enum rather than runtime metadata`` () =
        let generated = document (Schema.text |> Schema.constrain (Constraint.noneOf [ "admin"; "root" ]))

        test <@ generated.Contains "\"not\":{\"enum\":[\"admin\",\"root\"]}" @>
        test <@ not (generated.Contains "x-axial-runtime-constraints") @>

    [<Fact>]
    let ``notEqualTo lowers to a refused const`` () =
        // Previously runtime-only: the atom had a sound lowering, but `not` is one key per node and nothing
        // arbitrated between two rules wanting it. Exclusions are now merged, so the keyword can be published.
        let generated = document (Schema.int |> Schema.constrain (Constraint.notEqualTo 0))

        test <@ generated.Contains "\"not\":{\"const\":0}" @>
        test <@ not (generated.Contains "x-axial-runtime-constraints") @>

    [<Fact>]
    let ``notContains lowers to a refused contains on a collection`` () =
        let generated = document (Schema.listWith Schema.text |> Schema.constrain (Constraint.notContains "internal"))

        test <@ generated.Contains "\"not\":{\"contains\":{\"const\":\"internal\"}}" @>

    [<Fact>]
    let ``several exclusions on one value merge into one refused disjunction`` () =
        // Refusing `a or b` is refusing each of them, so the merge is exact rather than a weakening. Emitting two
        // `not` keys would produce a duplicate member and silently lose one of the rules.
        let schema =
            Schema.text
            |> Schema.constrain (Constraint.noneOf [ "admin" ])
            |> Schema.constrain (Constraint.notEqualTo "root")

        let generated = document schema

        test <@ generated.Contains "\"not\":{\"anyOf\":[{\"enum\":[\"admin\"]},{\"const\":\"root\"}]}" @>
        test <@ generated |> Seq.filter (fun character -> character = '{') |> Seq.length > 0 @>

        // One `not` member, not two.
        let occurrences =
            generated.Split "\"not\":" |> Array.length |> fun parts -> parts - 1

        test <@ occurrences = 1 @>

    [<Fact>]
    let ``an exclusion keeps its enforcement beside ordinary keywords`` () =
        let schema =
            Schema.text
            |> Schema.constrain (Constraint.maxLength 40)
            |> Schema.constrain (Constraint.noneOf [ "admin" ])

        let generated = document schema

        test <@ generated.Contains "\"maxLength\":40" @>
        test <@ generated.Contains "\"not\":{\"enum\":[\"admin\"]}" @>

    [<Fact>]
    let ``an exclusion whose operand has no injective wire encoding stays runtime-only`` () =
        // Same rule the including cases follow: two spellings of one instant are distinct on the wire and equal
        // after parsing, so wire refusal is not typed refusal.
        let instant = System.DateTimeOffset(2026, 8, 2, 0, 0, 0, System.TimeSpan.Zero)
        let generated = document (Schema.dateTime |> Schema.constrain (Constraint.noneOf [ instant ]))

        test <@ not (generated.Contains "\"not\":") @>
        test <@ generated.Contains "constraint.membership.noneOf" @>
