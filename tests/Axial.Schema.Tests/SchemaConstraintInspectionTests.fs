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
    let ``builder schema constraints are inspectable straight from the schema definition`` () =
        let emailValue =
            Schema.text
            |> Schema.constrainAll [ Constraint.required; Constraint.email; Constraint.maxLength 254 ]

        let ageValue = Schema.int |> Schema.constrain (Constraint.between 13 120)

        let schema =
            Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
            |> Schema.field "email" _.Email (emailValue |> Schema.constrainAll [ Constraint.required ])
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
    let ``builder schema constraints preserve per field ordering and metadata independent of a model instance`` () =
        let schema =
            Schema.recordFor<Address, _> (fun street city postalCode ->
                { Street = street
                  City = city
                  PostalCode = postalCode })
            |> Schema.field
                "street"
                _.Street
                (Schema.text |> Schema.constrain Constraint.required)
            |> Schema.field
                "city"
                _.City
                (Schema.text |> Schema.constrain (Constraint.lengthBetween 1 100))
            |> Schema.field
                "postalCode"
                _.PostalCode
                (Schema.text
                 |> Schema.constrainAll [ Constraint.required; Constraint.pattern "^[0-9]{5}$" ])
            |> Schema.build

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
