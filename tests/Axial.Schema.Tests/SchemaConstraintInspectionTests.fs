namespace Axial.Tests

open System
open System.Collections.Generic
open Axial.Schema
open Microsoft.FSharp.Reflection
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

/// <summary>
/// Proves that field-level and value-schema-level constraint metadata can be read straight from a
/// <c>Schema&lt;'model&gt;</c> definition produced by a constructor-last shape -- without constructing a
/// trusted model instance and without invoking any executable check or validation interpreter. Schema constraints are
/// portable data for interpreters such as diagnostics, JSON Schema, UI, and documentation generators, so they must
/// stay inspectable on their own.
/// </summary>
module ConstraintInspectionTests =
    type private Signup = { Email: string; Age: int }

    type private Address =
        { Street: string
          City: string
          PostalCode: string }

    let private modelDefinition (schema: Schema<'model>) =
        match schema.Definition with
        | ModelDefinition model -> model
        | PendingDefinition -> failwith "Expected public schema API to create a model definition."

    [<Fact>]
    let ``typed field DSL supports singular and plural constraints without metadata qualification`` () =
        let schema =
            schema<Signup> {
                field "email" _.Email {
                    withSchema Schema.text
                    constraints [ required; email; minLength 3; maxLength 254 ]
                    constrain trimmed
                }
                field "age" _.Age {
                    constrain (atLeast 13)
                    constraints [ atMost 120; notEqualTo 99 ]
                }
                construct (fun email age -> { Email = email; Age = age })
            }

        let fields = (modelDefinition schema).Fields
        let emailField = fields[0]
        let ageField = fields[1]

        test <@
            emailField.ValueSchema.Constraints |> List.map Constraint.metadata =
                [ ConstraintMetadata.Required
                  ConstraintMetadata.Email
                  ConstraintMetadata.MinLength 3
                  ConstraintMetadata.MaxLength 254
                  ConstraintMetadata.Trimmed ]
        @>

        test <@
            ageField.ValueSchema.Constraints |> List.map Constraint.metadata =
                [ ConstraintMetadata.AtLeast(box 13)
                  ConstraintMetadata.AtMost(box 120)
                  ConstraintMetadata.NotEqualTo(box 99) ]
        @>

    [<Fact>]
    let ``constraint constructors preserve the complete built-in metadata catalog`` () =
        let catalog : (Constraint * string * ConstraintMetadata) list =
            [ Constraint.required, "required", ConstraintMetadata.Required
              Constraint.optional, "optional", ConstraintMetadata.Optional
              Constraint.minLength 2, "minLength", ConstraintMetadata.MinLength 2
              Constraint.maxLength 20, "maxLength", ConstraintMetadata.MaxLength 20
              Constraint.lengthBetween 2 20, "lengthBetween", ConstraintMetadata.LengthBetween(2, 20)
              Constraint.email, "email", ConstraintMetadata.Email
              Constraint.trimmed, "trimmed", ConstraintMetadata.Trimmed
              Constraint.pattern "^[a-z]+$", "pattern", ConstraintMetadata.Pattern "^[a-z]+$"
              Constraint.oneOf [ "a"; "b" ], "oneOf", ConstraintMetadata.OneOf [ "a"; "b" ]
              Constraint.equalTo 3, "equalTo", ConstraintMetadata.EqualTo(box 3)
              Constraint.notEqualTo 3, "notEqualTo", ConstraintMetadata.NotEqualTo(box 3)
              Constraint.between 1 3, "between", ConstraintMetadata.Between(box 1, box 3)
              Constraint.greaterThan 1, "greaterThan", ConstraintMetadata.GreaterThan(box 1)
              Constraint.lessThan 3, "lessThan", ConstraintMetadata.LessThan(box 3)
              Constraint.atLeast 1, "atLeast", ConstraintMetadata.AtLeast(box 1)
              Constraint.atMost 3, "atMost", ConstraintMetadata.AtMost(box 3)
              Constraint.count 2, "count", ConstraintMetadata.Count 2
              Constraint.minCount 1, "minCount", ConstraintMetadata.MinCount 1
              Constraint.maxCount 3, "maxCount", ConstraintMetadata.MaxCount 3
              Constraint.countBetween 1 3, "countBetween", ConstraintMetadata.CountBetween(1, 3)
              Constraint.distinct, "distinct", ConstraintMetadata.Distinct
              Constraint.contains 2, "contains", ConstraintMetadata.Contains(box 2)
              Constraint.multipleOf 2, "multipleOf", ConstraintMetadata.MultipleOf(box 2)
              ((Axial.Check.Constraint.define "custom" [] (fun (_: int) -> Ok ())
                |> Constraint.fromCheck) :> Constraint),
              "custom",
              ConstraintMetadata.Custom("custom", Map.empty) ]

        catalog
        |> List.iter (fun (constraint', code, metadata) ->
            test <@ Constraint.code constraint' = code @>
            test <@ Constraint.metadata constraint' = metadata @>)

        let arguments = Constraint.arguments (Constraint.minLength 2) :?> IDictionary<string, obj>
        raises<NotSupportedException> <@ arguments.["minimum"] <- box 3 @>

        let representedCases =
            catalog
            |> List.map (fun (_, _, metadata) ->
                FSharpValue.GetUnionFields(metadata, typeof<ConstraintMetadata>)
                |> fst
                |> fun case -> case.Name)
            |> Set.ofList

        let declaredCases =
            FSharpType.GetUnionCases(typeof<ConstraintMetadata>)
            |> Array.map _.Name
            |> Set.ofArray

        test <@ representedCases = declaredCases @>

    [<Fact>]
    let ``shape schema constraints are inspectable straight from the schema definition`` () =
        let emailValue =
            Schema.text
            |> Schema.constrainAll [ Constraint.required; Constraint.email; Constraint.maxLength 254 ]

        let ageValue = Schema.int |> Schema.constrain (Constraint.between 13 120)

        let schema =
            schema<Signup> {
                field "email" _.Email {
                    withSchema (emailValue |> Schema.constrainAll [ Constraint.required ])
                }
                field "age" _.Age {
                    withSchema ageValue
                }
                construct (fun email age -> { Email = email; Age = age })
            }

        // Everything below reads metadata off `schema` alone: no `Signup` value is constructed, and no `Check` or
        // schema-interpreter function is called.
        let model = modelDefinition schema

        let byName =
            model.Fields
            |> List.map (fun field -> ExternalFieldName.value field.ExternalName, field)
            |> Map.ofList

        let email = byName["email"]
        let age = byName["age"]

        test <@ FieldOrder.value email.Order = 0 @>
        test <@ FieldOrder.value age.Order = 1 @>

        test <@ email.Constraints = [] @>
        test <@
            email.ValueSchema.Constraints |> List.map Constraint.code =
                [ "required"; "email"; "maxLength"; "required" ]
        @>
        test <@
            email.ValueSchema.Constraints |> List.map Constraint.metadata =
                [ ConstraintMetadata.Required
                  ConstraintMetadata.Email
                  ConstraintMetadata.MaxLength 254
                  ConstraintMetadata.Required ]
        @>

        test <@ age.Constraints |> List.isEmpty @>
        test <@ age.ValueSchema.Constraints |> List.map Constraint.code = [ "between" ] @>

        let ageRange = age.ValueSchema.Constraints.Head
        test <@ Constraint.tryFindArgument "minimum" ageRange = Some(box 13) @>
        test <@ Constraint.tryFindArgument "maximum" ageRange = Some(box 120) @>

    [<Fact>]
    let ``shape schema constraints preserve per field ordering and metadata independent of a model instance`` () =
        let schema =
            schema<Address> {
                field "street" _.Street {
                    withSchema (Schema.text |> Schema.constrain Constraint.required)
                }
                field "city" _.City {
                    withSchema (Schema.text |> Schema.constrain (Constraint.lengthBetween 1 100))
                }
                field "postalCode" _.PostalCode {
                    withSchema (
                        Schema.text
                        |> Schema.constrainAll [ Constraint.required; Constraint.pattern "^[0-9]{5}$" ]
                    )
                }
                construct (fun street city postalCode ->
                    { Street = street
                      City = city
                      PostalCode = postalCode })
            }

        let model = modelDefinition schema

        let constraintsByField =
            model.Fields
            |> List.map (fun field ->
                ExternalFieldName.value field.ExternalName, field.ValueSchema.Constraints |> List.map Constraint.code)

        test <@
            constraintsByField =
                [ "street", [ "required" ]
                  "city", [ "lengthBetween" ]
                  "postalCode", [ "required"; "pattern" ] ]
        @>

        let postal = model.Fields |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "postalCode")
        let patternConstraint = postal.ValueSchema.Constraints |> List.last

        test <@ Constraint.metadata patternConstraint = ConstraintMetadata.Pattern "^[0-9]{5}$" @>
        test <@ Constraint.tryFindArgument "pattern" patternConstraint = Some(box "^[0-9]{5}$") @>

    [<Fact>]
    let ``withMessage attaches a custom message without changing code, metadata, or arguments`` () =
        let required = Constraint.required
        let customized = required |> Constraint.withMessage "Email is required."

        test <@ Constraint.message required = None @>
        test <@ Constraint.message customized = Some "Email is required." @>
        test <@ Constraint.code customized = "required" @>
        test <@ Constraint.metadata customized = ConstraintMetadata.Required @>

        let maxLength = Constraint.maxLength 80 |> Constraint.withMessage "Too long."

        test <@ Constraint.message maxLength = Some "Too long." @>
        test <@ Constraint.tryFindArgument "maximum" maxLength = Some(box 80) @>
