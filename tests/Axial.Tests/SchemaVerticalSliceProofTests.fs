namespace Axial.Tests

open System
open System.Text
open Axial.ErrorHandling
open Axial.Schema
open Axial.Validation.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Proves the vertical schema metadata slice required by task line 163 of <c>dev-docs/TASKS.md</c> before RawInput,
/// schema validation, rules, or DSL work may start: one authored <c>Schema&lt;'model&gt;</c> instance must
/// simultaneously carry ordered fields, a primitive value schema, required and maxLength constraint metadata,
/// constraint lowering to <c>Check</c>, metadata inspection without running validation, constructor/getter alignment,
/// and enough typed information to compile a CodecMapper-style record plan. Earlier tests
/// (<c>SchemaConstraintCheckTests</c>, <c>SchemaConstraintInspectionTests</c>, <c>SchemaConstructorGetterAlignmentTests</c>,
/// <c>SchemaCompiledRecordPlanProofTests</c>) prove each capability in isolation; this test proves they compose on the
/// very same schema rather than only working for narrower, unrelated examples.
/// </summary>
module SchemaVerticalSliceProofTests =
    type private Signup = { Email: string; DisplayName: string }

    let private modelDefinition (schema: Schema<'model>) =
        match schema.Definition with
        | ModelDefinition model -> model
        | PendingDefinition -> failwith "Expected public schema API to create a model definition."

    /// Typed per-field decode/encode hook standing in for a real byte-level JSON codec, matching the shape proven in
    /// `SchemaCompiledRecordPlanProofTests`.
    type private FieldCodec<'value> = { Encode: 'value -> string; Decode: string -> 'value }

    type private CompiledField<'model, 'value> =
        { Order: int
          ExternalName: string
          ExternalNameUtf8: byte[]
          GetValue: 'model -> 'value
          Codec: FieldCodec<'value> }

    let private compile order (field: Field<'model, 'value>) (codec: FieldCodec<'value>) : CompiledField<'model, 'value> =
        let name = Field.externalName field |> ExternalFieldName.value

        { Order = order
          ExternalName = name
          ExternalNameUtf8 = Encoding.UTF8.GetBytes name
          GetValue = Field.getValue field
          Codec = codec }

    type private CompiledRecordPlan2<'model, 'a, 'b>
        (field1: CompiledField<'model, 'a>, field2: CompiledField<'model, 'b>, construct: 'a -> 'b -> 'model) =

        member _.Slots: (int * string * byte[]) array =
            [| field1.Order, field1.ExternalName, field1.ExternalNameUtf8
               field2.Order, field2.ExternalName, field2.ExternalNameUtf8 |]

        member _.Encode(model: 'model) : (string * string) array =
            [| field1.ExternalName, field1.Codec.Encode(field1.GetValue model)
               field2.ExternalName, field2.Codec.Encode(field2.GetValue model) |]

        member _.Decode(values: (string * string) array) : 'model =
            let mutable value1 = Unchecked.defaultof<'a>
            let mutable value2 = Unchecked.defaultof<'b>
            let mutable seen1 = false
            let mutable seen2 = false

            for name, raw in values do
                if name = field1.ExternalName then
                    value1 <- field1.Codec.Decode raw
                    seen1 <- true
                elif name = field2.ExternalName then
                    value2 <- field2.Codec.Decode raw
                    seen2 <- true

            if not seen1 then
                invalidArg (nameof values) $"Missing required field '{field1.ExternalName}'."

            if not seen2 then
                invalidArg (nameof values) $"Missing required field '{field2.ExternalName}'."

            construct value1 value2

    [<Fact>]
    let ``one authored schema proves ordering, primitive value schema, required/maxLength metadata, Check lowering, inspection, alignment, and a compiled plan together`` () =
        // Ordered fields + primitive value schema (`Value.text`) + required and maxLength constraint metadata,
        // authored through the progressive typed builder.
        let emailValue =
            Value.text |> Value.withConstraints [ SchemaConstraint.required; SchemaConstraint.maxLength 254 ]

        let displayNameValue = Value.text |> Value.withConstraint SchemaConstraint.required

        let emailField = Field.create "email" (fun (signup: Signup) -> signup.Email) emailValue
        let displayNameField = Field.create "displayName" (fun (signup: Signup) -> signup.DisplayName) displayNameValue

        // Declare fields in reverse of the record's own field order to prove constructor/getter alignment
        // follows declared argument position, not field name or a field's own pre-assigned default order.
        let schema =
            Schema.record (fun displayName email -> { Email = email; DisplayName = displayName })
            |> Schema.field "displayName" (fun (signup: Signup) -> signup.DisplayName) displayNameValue
            |> Schema.field "email" (fun (signup: Signup) -> signup.Email) emailValue
            |> Schema.build

        let source = { Email = "ada@example.com"; DisplayName = "Ada" }

        // Constructor/getter alignment: each getter still reads its own field from the trusted model regardless of
        // declaration position.
        test <@ Field.getValue displayNameField source = "Ada" @>
        test <@ Field.getValue emailField source = "ada@example.com" @>

        let model = modelDefinition schema
        let values = model.Fields |> List.map (fun field -> field.Getter source)

        test <@ model.Fields |> List.map (fun field -> ExternalFieldName.value field.ExternalName) = [ "displayName"; "email" ] @>
        test <@ model.Fields |> List.map (fun field -> FieldOrder.value field.Order) = [ 0; 1 ] @>
        test <@ values = [ box "Ada"; box "ada@example.com" ] @>
        test <@ ConstructorApplication.apply model.Constructor (values |> List.toArray) = source @>

        // Metadata inspection: constraint codes and typed metadata are readable straight from the schema definition,
        // without constructing a `Signup` value and without invoking any check or validation interpreter.
        let byName =
            model.Fields
            |> List.map (fun field -> ExternalFieldName.value field.ExternalName, field)
            |> Map.ofList

        let emailDescriptor = byName["email"]

        test <@ emailDescriptor.ValueSchema.Constraints |> List.map SchemaConstraint.code = [ "required"; "maxLength" ] @>
        test <@
            emailDescriptor.ValueSchema.Constraints |> List.map SchemaConstraint.metadata =
                [ SchemaConstraintMetadata.Required; SchemaConstraintMetadata.MaxLength 254 ]
        @>

        // Constraint lowering to `Check`: the same required/maxLength metadata read above lowers to an executable,
        // path-free value program.
        let emailCheck = SchemaConstraintCheck.text emailDescriptor.ValueSchema.Constraints

        test <@ emailCheck "ada@example.com" = Ok () @>
        test <@ emailCheck "" = Error [ Blank ] @>
        test <@ emailCheck (String.replicate 255 "a") = Error [ Length(MaximumLength 254, Some 255) ] @>

        // Compiled-record-plan proof: the same typed `Field<'model, 'value>` values used above compile into an
        // ordered, cached-name, typed-hook plan with direct constructor application -- no `obj array`, no per-value
        // reflection.
        let textCodec: FieldCodec<string> = { Encode = id; Decode = id }

        let plan =
            CompiledRecordPlan2(
                compile 0 displayNameField textCodec,
                compile 1 emailField textCodec,
                fun displayName email -> { Email = email; DisplayName = displayName }
            )

        test <@ plan.Slots |> Array.map (fun (order, name, _) -> order, name) = [| 0, "displayName"; 1, "email" |] @>
        test <@ plan.Slots |> Array.map (fun (_, _, utf8) -> Encoding.UTF8.GetString utf8) = [| "displayName"; "email" |] @>
        test <@ plan.Encode source = [| "displayName", "Ada"; "email", "ada@example.com" |] @>
        test <@ plan.Decode [| "email", "ada@example.com"; "displayName", "Ada" |] = source @>

        // The compiled plan and the type-erased inspection metadata agree on field order and external names, so the
        // codec-facing plan and the metadata-facing schema describe the same authored schema.
        test <@
            model.Fields |> List.map (fun field -> ExternalFieldName.value field.ExternalName) =
                (plan.Slots |> Array.toList |> List.map (fun (_, name, _) -> name))
        @>
