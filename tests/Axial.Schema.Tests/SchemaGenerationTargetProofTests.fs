namespace Axial.Tests

open Axial.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Compiles the exact code shape a future <c>[&lt;Schema&gt;]</c> source generator would emit (see
/// <c>dev-docs/current-ideas/schema-source-generation.md</c>), proving the generation target stays valid against the
/// public builder API: constructor/getter alignment from record field order, and attribute-style constraints lowering
/// to existing <c>SchemaConstraint</c> values. No generator tooling exists yet; this pins the target, not the tool.
/// </summary>
module SchemaGenerationTargetProofTests =
    type private Signup =
        { Email: string
          Age: int }

    // The hand-written equivalent of generated output for:
    //   [<Schema>] type Signup = { [<Required; MaxLength 254; Email>] Email: string; [<AtLeast 13>] Age: int }
    let private generatedSignupSchema () : Schema<Signup> =
        Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
        |> Schema.fieldWith
            [ SchemaConstraint.required; SchemaConstraint.maxLength 254; SchemaConstraint.email ]
            "email"
            _.Email
            Value.text
        |> Schema.fieldWith [ SchemaConstraint.atLeast 13 ] "age" _.Age Value.``int``
        |> Schema.build

    [<Fact>]
    let ``generation target shape compiles and exposes attribute constraints as schema metadata`` () =
        let description = Inspect.model (generatedSignupSchema ())

        test <@ description.Fields |> List.map _.Name = [ "email"; "age" ] @>

        let email = description.Fields |> List.find (fun field -> field.Name = "email")

        test
            <@
                email.Constraints |> List.map _.Metadata =
                    [ SchemaConstraintMetadata.Required
                      SchemaConstraintMetadata.MaxLength 254
                      SchemaConstraintMetadata.Email ]
            @>

        let age = description.Fields |> List.find (fun field -> field.Name = "age")
        test <@ age.Constraints |> List.map _.Metadata = [ SchemaConstraintMetadata.AtLeast(box 13) ] @>

    [<Fact>]
    let ``generation target aligns constructor arguments with getters by declaration order`` () =
        let description = Inspect.model (generatedSignupSchema ())

        test <@ description.Fields |> List.map _.Order = [ 0; 1 ] @>
