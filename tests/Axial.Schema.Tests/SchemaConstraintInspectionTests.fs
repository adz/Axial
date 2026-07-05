namespace Axial.Tests

open Axial.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Proves that field-level and value-schema-level constraint metadata can be read straight from a
/// <c>Schema&lt;'model&gt;</c> definition produced by the progressive builder -- without constructing a
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
    let ``builder schema constraints are inspectable straight from the schema definition`` () =
        let emailValue =
            Value.text
            |> Value.withConstraints [ SchemaConstraint.required; SchemaConstraint.email; SchemaConstraint.maxLength 254 ]

        let ageValue = Value.int |> Value.withConstraint (SchemaConstraint.between 13 120)

        let schema =
            Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
            |> Schema.fieldWith [ SchemaConstraint.required ] "email" _.Email emailValue
            |> Schema.field "age" _.Age ageValue
            |> Schema.build

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
    let ``builder schema constraints preserve per field ordering and metadata independent of a model instance`` () =
        let schema =
            Schema.recordFor<Address, _> (fun street city postalCode ->
                { Street = street
                  City = city
                  PostalCode = postalCode })
            |> Schema.field
                "street"
                _.Street
                (Value.text |> Value.withConstraint SchemaConstraint.required)
            |> Schema.field
                "city"
                _.City
                (Value.text |> Value.withConstraint (SchemaConstraint.lengthBetween 1 100))
            |> Schema.field
                "postalCode"
                _.PostalCode
                (Value.text
                 |> Value.withConstraints [ SchemaConstraint.required; SchemaConstraint.pattern "^[0-9]{5}$" ])
            |> Schema.build

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

    [<Fact>]
    let ``withMessage attaches a custom message without changing code, metadata, or arguments`` () =
        let required = SchemaConstraint.required
        let customized = required |> SchemaConstraint.withMessage "Email is required."

        test <@ SchemaConstraint.message required = None @>
        test <@ SchemaConstraint.message customized = Some "Email is required." @>
        test <@ SchemaConstraint.code customized = "required" @>
        test <@ SchemaConstraint.metadata customized = SchemaConstraintMetadata.Required @>

        let maxLength = SchemaConstraint.maxLength 80 |> SchemaConstraint.withMessage "Too long."

        test <@ SchemaConstraint.message maxLength = Some "Too long." @>
        test <@ SchemaConstraint.tryFindArgument "maximum" maxLength = Some(box 80) @>
