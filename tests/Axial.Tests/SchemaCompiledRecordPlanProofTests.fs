namespace Axial.Tests

open System
open System.Text
open Axial.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Proves that today's progressive <c>Schema</c> builder carries enough typed information to lower a flat
/// record schema into a CodecMapper-style compiled record plan: ordered fields, cached UTF-8 external names, typed
/// per-field decode/encode hooks, indexed field slots, and direct constructor application. Compare
/// <c>CompiledRecordPlan2</c> below against CodecMapper's <c>RecordDecoder2&lt;'T,'A,'B&gt;</c> in
/// <c>JsonTypedRecordDecode.fs</c>: same ordered-field-chain shape, same typed locals, same single direct constructor
/// call, but built from portable functions instead of runtime reflection or `Expression.Compile`.
/// </summary>
/// <remarks>
/// This lives as a test-only prototype rather than new `Axial.Schema` source. Task line 148 of `dev-docs/TASKS.md`
/// asks for proof the lowering is possible before codec work starts and before `Axial.Schema` commits to a codec
/// package boundary (see Phase 14). The key finding this test records: a zero-boxing compiled plan must be built
/// from the typed field chain appended by `Schema.field`, not from
/// the type-erased `Schema&lt;'model&gt;.Definition` that metadata interpreters use. That erased shape stores fields
/// as `'model -> obj` getters and applies constructors through `obj array`, which is the right trade for
/// arity-independent inspection but is exactly the `obj array` dispatch a codec hot path must avoid.
/// </remarks>
module SchemaCompiledRecordPlanProofTests =

    /// Typed per-field decode/encode hook standing in for a real byte-level JSON codec. A plain pair of functions:
    /// no reflection, so it stays AOT- and Fable-compatible without conditional compilation.
    type private FieldCodec<'value> =
        { Encode: 'value -> string
          Decode: string -> 'value }

    module private FieldCodec =
        let text: FieldCodec<string> = { Encode = id; Decode = id }
        let int: FieldCodec<int> = { Encode = string; Decode = Int32.Parse }

    /// One compiled field slot: order-indexed metadata plus the typed getter/codec hot-path hooks. Caching the
    /// UTF-8 external name here is the one piece that is genuinely .NET-only; a Fable target would keep the plain
    /// string name and skip the byte cache rather than needing a different plan shape.
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

    /// A compiled plan for a flat two-field record. Field slots stay strongly typed as distinct members -- mirroring
    /// CodecMapper's `RecordDecoder2<'T,'A,'B>` -- so `Decode` walks an ordered field chain and calls the two-argument
    /// constructor directly with typed locals. There is no `obj array`, no per-value reflection, and no generic
    /// dictionary keyed by field name on the decode path.
    type private CompiledRecordPlan2<'model, 'a, 'b>
        (field1: CompiledField<'model, 'a>, field2: CompiledField<'model, 'b>, construct: 'a -> 'b -> 'model) =

        /// Order-indexed slots for interpreters that only need name/order/UTF-8 bytes (docs, JSON Schema).
        member _.Slots: (int * string * byte[]) array =
            [| field1.Order, field1.ExternalName, field1.ExternalNameUtf8
               field2.Order, field2.ExternalName, field2.ExternalNameUtf8 |]

        member _.Encode(model: 'model) : (string * string) array =
            [| field1.ExternalName, field1.Codec.Encode(field1.GetValue model)
               field2.ExternalName, field2.Codec.Encode(field2.GetValue model) |]

        /// Ordered field-name chain, exactly like CodecMapper's byte-level property match chain, followed by one
        /// direct typed constructor call.
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

    /// Compiles a plan from typed `Field<'model, 'value>` values matching the builder-declared schema --
    /// proving the lowering reuses existing public schema metadata rather than a parallel schema representation.
    let private compileMap2
        (construct: 'a -> 'b -> 'model)
        (left: Field<'model, 'a>)
        (leftCodec: FieldCodec<'a>)
        (right: Field<'model, 'b>)
        (rightCodec: FieldCodec<'b>)
        =
        CompiledRecordPlan2(compile 0 left leftCodec, compile 1 right rightCodec, construct)

    type private Contact = { Name: string; Age: int }

    let private modelDefinition (schema: Schema<'model>) =
        match schema.Definition with
        | ModelDefinition model -> model
        | PendingDefinition -> failwith "Expected public schema API to create a model definition."

    [<Fact>]
    let ``flat record schema lowers to a compiled plan with ordered fields, cached UTF-8 names, and typed hooks`` () =
        let nameField = Field.create "name" (fun (contact: Contact) -> contact.Name) Value.text
        let ageField = Field.create "age" (fun (contact: Contact) -> contact.Age) Value.``int``

        let schema =
            Schema.record (fun name age -> { Name = name; Age = age })
            |> Schema.field "name" (fun (contact: Contact) -> contact.Name) Value.text
            |> Schema.field "age" (fun (contact: Contact) -> contact.Age) Value.``int``
            |> Schema.build

        let plan =
            compileMap2 (fun name age -> { Name = name; Age = age }) nameField FieldCodec.text ageField FieldCodec.int

        let contact = { Name = "Ada"; Age = 36 }

        test <@ plan.Slots |> Array.map (fun (order, name, _) -> order, name) = [| 0, "name"; 1, "age" |] @>
        test <@ plan.Slots |> Array.map (fun (_, _, utf8) -> Encoding.UTF8.GetString utf8) = [| "name"; "age" |] @>

        // Direct typed constructor application round-trips without ever boxing a field value or building an
        // `obj array`.
        test <@ plan.Encode contact = [| "name", "Ada"; "age", "36" |] @>
        test <@ plan.Decode [| "name", "Ada"; "age", "36" |] = contact @>

        // Field order in the raw source must not matter: the compiled plan matches by name, not position.
        test <@ plan.Decode [| "age", "36"; "name", "Ada" |] = contact @>

        // The compiled plan and the type-erased `Schema<'model>` inspection definition agree on field order and
        // external names, so both describe the same declared shape.
        let fields = modelDefinition schema

        test <@
            fields.Fields |> List.map (fun field -> ExternalFieldName.value field.ExternalName) =
                (plan.Slots |> Array.toList |> List.map (fun (_, name, _) -> name))
        @>

    [<Fact>]
    let ``decode raises when a required field is missing instead of partially applying the constructor`` () =
        let nameField = Field.create "name" (fun (contact: Contact) -> contact.Name) Value.text
        let ageField = Field.create "age" (fun (contact: Contact) -> contact.Age) Value.``int``

        let plan =
            compileMap2 (fun name age -> { Name = name; Age = age }) nameField FieldCodec.text ageField FieldCodec.int

        raises<ArgumentException> <@ plan.Decode [| "name", "Ada" |] @>
