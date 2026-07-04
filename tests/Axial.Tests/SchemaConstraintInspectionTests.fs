namespace Axial.Tests

open Axial.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Proves that field-level and value-schema-level constraint metadata can be read straight from a
/// <c>Schema&lt;'model&gt;</c> definition produced by <c>Schema.map2</c> / <c>Schema.map3</c> -- without constructing a
/// trusted model instance and without invoking any executable check or validation interpreter. Schema constraints are
/// portable data for interpreters such as diagnostics, JSON Schema, UI, and documentation generators, so they must
/// stay inspectable on their own.
/// </summary>
module SchemaConstraintInspectionTests =
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
    let ``map2 schema constraints are inspectable straight from the schema definition`` () =
        let emailValue =
            Value.text
            |> Value.withConstraints [ SchemaConstraint.required; SchemaConstraint.email; SchemaConstraint.maxLength 254 ]

        let ageValue = Value.``int`` |> Value.withConstraint (SchemaConstraint.between 13 120)

        let emailField =
            Schema.field "email" (fun (signup: Signup) -> signup.Email) emailValue
            |> Field.withConstraint SchemaConstraint.required

        let ageField = Schema.field "age" (fun (signup: Signup) -> signup.Age) ageValue

        let schema = Schema.map2 (fun email age -> { Email = email; Age = age }) emailField ageField

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

        test <@ email.Constraints |> List.map SchemaConstraint.code = [ "required" ] @>
        test <@
            email.ValueSchema.Constraints |> List.map SchemaConstraint.code =
                [ "required"; "email"; "maxLength" ]
        @>
        test <@
            email.ValueSchema.Constraints |> List.map SchemaConstraint.metadata =
                [ SchemaConstraintMetadata.Required
                  SchemaConstraintMetadata.Email
                  SchemaConstraintMetadata.MaxLength 254 ]
        @>

        test <@ age.Constraints |> List.isEmpty @>
        test <@ age.ValueSchema.Constraints |> List.map SchemaConstraint.code = [ "between" ] @>

        let ageRange = age.ValueSchema.Constraints.Head
        test <@ SchemaConstraint.tryFindArgument "minimum" ageRange = Some(box 13) @>
        test <@ SchemaConstraint.tryFindArgument "maximum" ageRange = Some(box 120) @>

    [<Fact>]
    let ``map3 schema constraints preserve per field ordering and metadata independent of a model instance`` () =
        let street =
            Schema.field
                "street"
                (fun (address: Address) -> address.Street)
                (Value.text |> Value.withConstraint SchemaConstraint.required)

        let city =
            Schema.field
                "city"
                (fun (address: Address) -> address.City)
                (Value.text |> Value.withConstraint (SchemaConstraint.lengthBetween 1 100))

        let postalCode =
            Schema.field
                "postalCode"
                (fun (address: Address) -> address.PostalCode)
                (Value.text
                 |> Value.withConstraints [ SchemaConstraint.required; SchemaConstraint.pattern "^[0-9]{5}$" ])

        let schema =
            Schema.map3
                (fun street city postalCode -> { Street = street; City = city; PostalCode = postalCode })
                street
                city
                postalCode

        let model = modelDefinition schema

        let constraintsByField =
            model.Fields
            |> List.map (fun field ->
                ExternalFieldName.value field.ExternalName, field.ValueSchema.Constraints |> List.map SchemaConstraint.code)

        test <@
            constraintsByField =
                [ "street", [ "required" ]
                  "city", [ "lengthBetween" ]
                  "postalCode", [ "required"; "pattern" ] ]
        @>

        let postal = model.Fields |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "postalCode")
        let patternConstraint = postal.ValueSchema.Constraints |> List.last

        test <@ SchemaConstraint.metadata patternConstraint = SchemaConstraintMetadata.Pattern "^[0-9]{5}$" @>
        test <@ SchemaConstraint.tryFindArgument "pattern" patternConstraint = Some(box "^[0-9]{5}$") @>
