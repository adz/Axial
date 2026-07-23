namespace Axial.Tests

open Axial

open System
open System.Text
open Axial.ErrorHandling
open Axial.Schema
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

/// <summary>
/// Proves the vertical schema metadata slice required by task line 163 of <c>dev-docs/TASKS.md</c> before Data,
/// schema validation, rules, or syntax work may start: one authored <c>Schema&lt;'model&gt;</c> instance must
/// simultaneously carry ordered fields, a primitive value schema, required and maxLength constraint metadata,
/// constraint lowering to <c>Check</c>, metadata inspection without running validation, constructor/getter alignment,
/// and enough typed information to compile a CodecMapper-style record plan. Earlier tests
/// (<c>ConstraintCheckTests</c>, <c>ConstraintInspectionTests</c>, <c>SchemaConstructorGetterAlignmentTests</c>,
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

    type private ICompiledPlan<'model> =
        abstract member Slots: (int * string * byte[]) array
        abstract member Encode: 'model -> (string * string) array
        abstract member Decode: (string * string) array -> 'model

    type private ICompiledChain<'model, 'constructorIn, 'constructorOut> =
        abstract member Slots: (int * string * byte[]) list
        abstract member Encode: 'model -> (string * string) list
        abstract member Reset: unit -> unit
        abstract member TryCollect: name: string * raw: string -> bool
        abstract member ApplyCollected: 'constructorIn -> 'constructorOut

    type private CompiledFieldsEmpty<'model, 'constructor>() =
        interface ICompiledChain<'model, 'constructor, 'constructor> with
            member _.Slots = []
            member _.Encode(_) = []
            member _.Reset() = ()
            member _.TryCollect(_, _) = false
            member _.ApplyCollected(constructor) = constructor

    type private CompiledFieldsCons<'model, 'constructorIn, 'field, 'next, 'head
        when 'head :> ICompiledChain<'model, 'constructorIn, 'field -> 'next>>
        (
            head: 'head,
            field: CompiledField<'model, 'field>
        ) =

        let mutable collectedValue: 'field voption = ValueNone

        interface ICompiledChain<'model, 'constructorIn, 'next> with
            member _.Slots = head.Slots @ [ field.Order, field.ExternalName, field.ExternalNameUtf8 ]
            member _.Encode(model) = head.Encode model @ [ field.ExternalName, field.Codec.Encode(field.GetValue model) ]

            member _.Reset() =
                head.Reset()
                collectedValue <- ValueNone

            member _.TryCollect(name, raw) =
                if name = field.ExternalName then
                    collectedValue <- ValueSome(field.Codec.Decode raw)
                    true
                else
                    head.TryCollect(name, raw)

            member _.ApplyCollected(constructor) =
                let constructorForField = head.ApplyCollected constructor

                match collectedValue with
                | ValueSome value -> constructorForField value
                | ValueNone -> invalidArg "values" $"Missing required field '{field.ExternalName}'."

    type private CompiledRecordPlan<'model, 'constructor, 'constructed>
        (
            constructor: 'constructor,
            chain: ICompiledChain<'model, 'constructor, 'constructed>,
            finish: 'constructed -> Result<'model, string>
        ) =

        interface ICompiledPlan<'model> with
            member _.Slots = chain.Slots |> List.toArray
            member _.Encode(model) = chain.Encode model |> List.toArray

            member _.Decode(values) =
                chain.Reset()

                for name, raw in values do
                    chain.TryCollect(name, raw) |> ignore

                match finish (chain.ApplyCollected constructor) with
                | Ok model -> model
                | Error message -> invalidOp message

    type private PlanChainResult<'model, 'constructorIn, 'constructorOut>(value: obj) =
        interface IRecordPlanState<'model, 'constructorIn, 'constructorOut> with
            member _.Value = value

    module private FieldCodecs =
        let forField<'value> () : FieldCodec<'value> =
            if typeof<'value> = typeof<string> then
                box { Encode = id; Decode = id } :?> FieldCodec<'value>
            else
                invalidArg "field" $"No proof codec registered for field type {typeof<'value>.FullName}."

    type private CompiledRecordPlanFactory<'model>() =
        interface IRecordPlanCompiler<'model, ICompiledPlan<'model>> with
            member _.OnEnd() =
                let chain = CompiledFieldsEmpty<'model, 'constructor>() :> ICompiledChain<'model, 'constructor, 'constructor>
                PlanChainResult<'model, 'constructor, 'constructor>(box chain) :> IRecordPlanState<_, _, _>

            member _.OnField(order, field: Field<'model, 'field>, head) =
                let headChain = head.Value :?> ICompiledChain<'model, 'constructorIn, 'field -> 'next>
                let right = compile order field (FieldCodecs.forField<'field> ())
                let chain =
                    CompiledFieldsCons<'model, 'constructorIn, 'field, 'next, _>(headChain, right)
                    :> ICompiledChain<'model, 'constructorIn, 'next>

                PlanChainResult<'model, 'constructorIn, 'next>(box chain) :> IRecordPlanState<_, _, _>

            member _.OnComplete<'constructor, 'constructed>
                (
                    constructor: 'constructor,
                    chain: IRecordPlanState<'model, 'constructor, 'constructed>,
                    finish: 'constructed -> Result<'model, string>
                ) =
                let compiledChain = chain.Value :?> ICompiledChain<'model, 'constructor, 'constructed>
                CompiledRecordPlan<'model, 'constructor, 'constructed>(constructor, compiledChain, finish)
                :> ICompiledPlan<'model>

    let private compileFromSchema (schema: Schema<'model>) =
        Schema.compilePlan (CompiledRecordPlanFactory<'model>()) schema

    [<Fact>]
    let ``one authored schema proves ordering, primitive value schema, required/maxLength metadata, Check lowering, inspection, alignment, and a compiled plan together`` () =
        // Ordered fields + primitive value schema (`Schema.text`) + required and maxLength constraint metadata,
        // authored through the constructor-last typed shape.
        let emailValue =
            Schema.text |> Schema.constrainAll [ Constraint.required; Constraint.maxLength 254 ]

        let displayNameValue = Schema.text |> Schema.constrain Constraint.required

        // Declare fields in reverse of the record's own field order to prove constructor/getter alignment
        // follows declared argument position, not the record's source order or external field name.
        let schema =
            schema<Signup> {
                field "displayName" _.DisplayName {
                    withSchema displayNameValue
                }
                field "email" _.Email {
                    withSchema emailValue
                }
                construct (fun displayName email -> { Email = email; DisplayName = displayName })
            }

        let source = { Email = "ada@example.com"; DisplayName = "Ada" }

        // Constructor/getter alignment: each getter still reads its own field from the trusted model regardless of
        // declaration position.
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

        test <@ emailDescriptor.ValueSchema.Constraints |> List.map Constraint.code = [ "required"; "maxLength" ] @>
        test <@
            emailDescriptor.ValueSchema.Constraints |> List.map Constraint.metadata =
                [ ConstraintMetadata.Required; ConstraintMetadata.MaxLength 254 ]
        @>

        // Constraint lowering to `Check`: the same required/maxLength metadata read above lowers to an executable,
        // path-free value program.
        let emailCheck = ConstraintCheck.text emailDescriptor.ValueSchema.Constraints

        test <@ emailCheck "ada@example.com" = Ok "ada@example.com" @>
        test <@ emailCheck "" = Error [ Required ] @>
        test <@ emailCheck (String.replicate 255 "a") = Error [ InvalidLength(MaximumLength 254, Some 255) ] @>

        // Compiled-record-plan proof: the same built `Schema<'model>` value compiles into an ordered, cached-name,
        // typed-hook plan with direct constructor application -- no `obj array`, no per-value reflection, and no
        // caller re-supplying the constructor or standalone typed fields.
        let plan = compileFromSchema schema

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
