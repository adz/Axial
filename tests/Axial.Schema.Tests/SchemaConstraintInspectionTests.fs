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
                    constraints [ supplied; present; email; minLength 3; maxLength 254 ]
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
                [ (ConstraintMetadata.Supply Supply.Supplied)
                  (ConstraintMetadata.ValueConstraint Axial.Check.ConstraintMetadata.Present)
                  (ConstraintMetadata.ValueConstraint Axial.Check.ConstraintMetadata.Email)
                  ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.MinLength 3)
                  ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.MaxLength 254)
                  (ConstraintMetadata.ValueConstraint Axial.Check.ConstraintMetadata.Trimmed) ]
        @>

        test <@
            ageField.ValueSchema.Constraints |> List.map Constraint.metadata =
                [ ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.AtLeast(box 13))
                  ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.AtMost(box 120))
                  ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.NotEqualTo(box 99)) ]
        @>

    [<Fact>]
    let ``constraint constructors preserve the complete built-in metadata catalog`` () =
        let catalog : (ConstraintDescriptor * string * ConstraintMetadata) list =
            [ Constraint.supplied, "supplied", (ConstraintMetadata.Supply Supply.Supplied)
              ((Constraint.present: SchemaConstraint<string>) :> ConstraintDescriptor), "present", (ConstraintMetadata.ValueConstraint Axial.Check.ConstraintMetadata.Present)
              Constraint.omittable, "omittable", (ConstraintMetadata.Supply Supply.Omittable)
              ((Constraint.length 2: SchemaConstraint<string>) :> ConstraintDescriptor), "length", ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.Length 2)
              ((Constraint.minLength 2: SchemaConstraint<string>) :> ConstraintDescriptor), "minLength", ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.MinLength 2)
              ((Constraint.maxLength 20: SchemaConstraint<string>) :> ConstraintDescriptor), "maxLength", ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.MaxLength 20)
              ((Constraint.lengthBetween 2 20: SchemaConstraint<string>) :> ConstraintDescriptor), "lengthBetween", ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.LengthBetween(2, 20))
              Constraint.email, "email", (ConstraintMetadata.ValueConstraint Axial.Check.ConstraintMetadata.Email)
              Constraint.trimmed, "trimmed", (ConstraintMetadata.ValueConstraint Axial.Check.ConstraintMetadata.Trimmed)
              Constraint.pattern "^[a-z]+$", "pattern", ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.Pattern "^[a-z]+$")
              Constraint.oneOf [ "a"; "b" ], "oneOf", ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.OneOf [ "a"; "b" ])
              Constraint.equalTo 3, "equalTo", ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.EqualTo(box 3))
              Constraint.notEqualTo 3, "notEqualTo", ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.NotEqualTo(box 3))
              Constraint.between 1 3, "between", ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.Between(box 1, box 3))
              Constraint.greaterThan 1, "greaterThan", ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.GreaterThan(box 1))
              Constraint.lessThan 3, "lessThan", ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.LessThan(box 3))
              Constraint.atLeast 1, "atLeast", ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.AtLeast(box 1))
              Constraint.atMost 3, "atMost", ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.AtMost(box 3))
              ((Constraint.minLength 1: SchemaConstraint<string>) :> ConstraintDescriptor), "minLength", ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.MinLength 1)
              ((Constraint.maxLength 3: SchemaConstraint<string>) :> ConstraintDescriptor), "maxLength", ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.MaxLength 3)
              Constraint.distinct, "distinct", (ConstraintMetadata.ValueConstraint Axial.Check.ConstraintMetadata.Distinct)
              Constraint.contains 2, "contains", ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.Contains(box 2))
              Constraint.multipleOf 2, "multipleOf", ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.MultipleOf(box 2))
              ((Axial.Check.Constraint.define "custom" [] (fun (_: int) -> Ok ())
                |> Constraint.fromCheck) :> ConstraintDescriptor),
              "custom",
              ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.Custom("custom", Map.empty)) ]

        catalog
        |> List.iter (fun (constraint', code, metadata) ->
            test <@ Constraint.code constraint' = code @>
            test <@ Constraint.metadata constraint' = metadata @>)

        let arguments = Constraint.arguments (Constraint.minLength 2: SchemaConstraint<string>) :?> IDictionary<string, obj>
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
            |> Schema.constrainAll [ Constraint.present; Constraint.email; Constraint.maxLength 254 ]

        let ageValue = Schema.int |> Schema.constrain (Constraint.between 13 120)

        let schema =
            schema<Signup> {
                field "email" _.Email {
                    withSchema (emailValue |> Schema.constrainAll [ Constraint.present ])
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
                [ "present"; "email"; "maxLength"; "present" ]
        @>
        test <@
            email.ValueSchema.Constraints |> List.map Constraint.metadata =
                [ (ConstraintMetadata.ValueConstraint Axial.Check.ConstraintMetadata.Present)
                  (ConstraintMetadata.ValueConstraint Axial.Check.ConstraintMetadata.Email)
                  ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.MaxLength 254)
                  (ConstraintMetadata.ValueConstraint Axial.Check.ConstraintMetadata.Present) ]
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
                    withSchema (Schema.text |> Schema.constrain Constraint.present)
                }
                field "city" _.City {
                    withSchema (Schema.text |> Schema.constrain (Constraint.lengthBetween 1 100))
                }
                field "postalCode" _.PostalCode {
                    withSchema (
                        Schema.text
                        |> Schema.constrainAll [ Constraint.present; Constraint.pattern "^[0-9]{5}$" ]
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
                [ "street", [ "present" ]
                  "city", [ "lengthBetween" ]
                  "postalCode", [ "present"; "pattern" ] ]
        @>

        let postal = model.Fields |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "postalCode")
        let patternConstraint = postal.ValueSchema.Constraints |> List.last

        test <@ Constraint.metadata patternConstraint = ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.Pattern "^[0-9]{5}$") @>
        test <@ Constraint.tryFindArgument "pattern" patternConstraint = Some(box "^[0-9]{5}$") @>

    [<Fact>]
    let ``withMessage attaches a custom message without changing code, metadata, or arguments`` () =
        let supplied = Constraint.supplied
        let customized = supplied |> Constraint.withMessage "Email must be supplied."

        test <@ Constraint.message supplied = None @>
        test <@ Constraint.message customized = Some "Email must be supplied." @>
        test <@ Constraint.code customized = "supplied" @>
        test <@ Constraint.metadata customized = (ConstraintMetadata.Supply Supply.Supplied) @>

        let maxLength: SchemaConstraint<string> = Constraint.maxLength 80 |> Constraint.withMessage "Too long."

        test <@ Constraint.message maxLength = Some "Too long." @>
        test <@ Constraint.tryFindArgument "maximum" maxLength = Some(box 80) @>
