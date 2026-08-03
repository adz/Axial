namespace Axial.Tests

open Axial.Constraint
open Axial.Schema
open Xunit
open Axial.Schema.Syntax
open Axial.Constraint.ConstraintDSL
open Swensen.Unquote

/// <summary>
/// Compiles the exact code shape a future <c>[&lt;Schema&gt;]</c> source generator would emit (see
/// <c>dev-docs/current-ideas/schema-source-generation.md</c>), proving the generation target stays valid against the
/// public constructor-last API: constructor/getter alignment from record field order, and attribute-style constraints lowering
/// to existing <c>Constraint</c> values. No generator tooling exists yet; this pins the target, not the tool.
/// </summary>
module SchemaGenerationTargetProofTests =
    type private Signup =
        { Email: string
          Age: int }

    // The hand-written equivalent of generated output for:
    //   [<Schema>] type Signup = { [<Blank; MaxLength 254; Email>] Email: string; [<AtLeast 13>] Age: int }
    let private generatedSignupSchema () : Schema<Signup> =
        schema<Signup> {
            field "email" _.Email {
                withSchema (Schema.text |> Schema.constrainAll [ Constraint.present; Constraint.maxLength 254; Constraint.email ])
            }
            field "age" _.Age {
                withSchema (Schema.int |> Schema.constrainAll [ Constraint.atLeast 13 ])
            }
            construct (fun email age -> { Email = email; Age = age })
        }

    [<Fact>]
    let ``generation target shape compiles and exposes attribute constraints as schema metadata`` () =
        let description = Inspect.model (generatedSignupSchema ())

        test <@ description.Fields |> List.map _.Name = [ "email"; "age" ] @>

        let email = description.Fields |> List.find (fun field -> field.Name = "email")

        test
            <@
                email.Schema.Constraints |> List.collect ConstraintDescription.atoms =
                    [ PresenceAtom Present; CardinalityAtom(Cardinality.Maximum 254); FormatAtom Email ]
            @>

        let age = description.Fields |> List.find (fun field -> field.Name = "age")

        test <@
            age.Schema.Constraints |> List.collect ConstraintDescription.atoms =
                [ RelationAtom(Compared(AtLeast, ConstraintValue.Integer 13L)) ]
        @>

    [<Fact>]
    let ``generation target aligns constructor arguments with getters by declaration order`` () =
        let description = Inspect.model (generatedSignupSchema ())

        test <@ description.Fields |> List.map _.Order = [ 0; 1 ] @>
