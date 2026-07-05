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
/// This lives as a test-only prototype rather than new `Axial.Schema` source. The key finding this test records: a
/// zero-boxing compiled plan can be built from the typed field chain retained by the built
/// <c>Schema&lt;'model&gt;</c> value itself, not from caller-supplied fields and constructors and not from the
/// type-erased <c>Schema&lt;'model&gt;.Definition</c> that metadata interpreters use. That erased shape stores fields as
/// <c>'model -> obj</c> getters and applies constructors through <c>obj array</c>, which is the right trade for
/// arity-independent inspection but is exactly the <c>obj array</c> dispatch a codec hot path must avoid.
/// </remarks>
module SchemaCompiledRecordPlanProofTests =

    /// Typed per-field decode/encode hook standing in for a real byte-level JSON codec. A plain pair of functions:
    /// no reflection, so it stays AOT- and Fable-compatible without conditional compilation.
    type private FieldCodec<'value> =
        { Encode: 'value -> string
          Decode: string -> 'value }

    module private FieldCodecValues =
        let text: FieldCodec<string> = { Encode = id; Decode = id }
        let int: FieldCodec<int> = { Encode = string; Decode = Int32.Parse }

    type private ICompiledPlan<'model> =
        abstract member Slots: (int * string * byte[]) array
        abstract member Encode: 'model -> (string * string) array
        abstract member Decode: (string * string) array -> 'model

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

    module private FieldCodecs =
        let forField<'value> () : FieldCodec<'value> =
            if typeof<'value> = typeof<string> then
                box FieldCodecValues.text :?> FieldCodec<'value>
            elif typeof<'value> = typeof<int> then
                box FieldCodecValues.int :?> FieldCodec<'value>
            else
                invalidArg "field" $"No proof codec registered for field type {typeof<'value>.FullName}."

    type private ICompiledChain<'model, 'constructorIn, 'constructorOut> =
        abstract member Slots: (int * string * byte[]) list
        abstract member Encode: 'model -> (string * string) list
        abstract member Reset: unit -> unit
        abstract member TryCollect: name: string * raw: string -> bool
        abstract member ApplyCollected: 'constructorIn -> 'constructorOut

    type private CompiledFieldsEnd<'model, 'constructor>() =
        interface ICompiledChain<'model, 'constructor, 'constructor> with
            member _.Slots = []
            member _.Encode(_) = []
            member _.Reset() = ()
            member _.TryCollect(_, _) = false
            member _.ApplyCollected(constructor) = constructor

    type private CompiledFieldsAppend<'model, 'constructorIn, 'field, 'next, 'head
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

    /// A compiled plan for a flat record. Field slots stay strongly typed in the compiled chain, so `Decode` walks an
    /// ordered field-name chain and then applies the original curried constructor directly. There is no `obj array`, no
    /// per-value reflection, and no generic dictionary keyed by field name on the decode path.
    type private CompiledRecordPlan<'model, 'constructor>
        (constructor: 'constructor, chain: ICompiledChain<'model, 'constructor, 'model>) =

        interface ICompiledPlan<'model> with
            member _.Slots = chain.Slots |> List.toArray
            member _.Encode(model) = chain.Encode model |> List.toArray

            member _.Decode(values) =
                chain.Reset()

                for name, raw in values do
                    chain.TryCollect(name, raw) |> ignore

                chain.ApplyCollected constructor

    type private PlanChainResult<'model, 'constructorIn, 'constructorOut>(value: obj) =
        interface IFieldChainResult<'model, 'constructorIn, 'constructorOut> with
            member _.Value = value

    /// Compiles a plan from the typed field chain retained by the built `Schema<'model>`. The generic factory methods
    /// see each field's real value type and the original curried constructor, so the resulting decode path applies the
    /// constructor directly through typed chain nodes rather than through `obj array`.
    type private CompiledRecordPlanFactory<'model>() =
        interface IFieldChainFactory<'model, ICompiledPlan<'model>> with
            member _.OnEnd() =
                let chain = CompiledFieldsEnd<'model, 'constructor>() :> ICompiledChain<'model, 'constructor, 'constructor>
                PlanChainResult<'model, 'constructor, 'constructor>(box chain) :> IFieldChainResult<_, _, _>

            member _.OnField(order, field: Field<'model, 'field>, head) =
                let headChain = head.Value :?> ICompiledChain<'model, 'constructorIn, 'field -> 'next>
                let right = compile order field (FieldCodecs.forField<'field> ())
                let chain =
                    CompiledFieldsAppend<'model, 'constructorIn, 'field, 'next, _>(headChain, right)
                    :> ICompiledChain<'model, 'constructorIn, 'next>

                PlanChainResult<'model, 'constructorIn, 'next>(box chain) :> IFieldChainResult<_, _, _>

            member _.OnComplete<'constructor>
                (
                    constructor: 'constructor,
                    chain: IFieldChainResult<'model, 'constructor, 'model>
                ) =
                let compiledChain = chain.Value :?> ICompiledChain<'model, 'constructor, 'model>
                CompiledRecordPlan<'model, 'constructor>(constructor, compiledChain) :> ICompiledPlan<'model>

    let private compileFromSchema (schema: Schema<'model>) =
        Schema.specialize (CompiledRecordPlanFactory<'model>()) schema

    type private Contact = { Name: string; Age: int }

    let private modelDefinition (schema: Schema<'model>) =
        match schema.Definition with
        | ModelDefinition model -> model
        | PendingDefinition -> failwith "Expected public schema API to create a model definition."

    [<Fact>]
    let ``flat record schema lowers to a compiled plan with ordered fields, cached UTF-8 names, and typed hooks`` () =
        let schema =
            Schema.recordFor<Contact, _> (fun name age -> { Name = name; Age = age })
            |> Schema.text "name" _.Name
            |> Schema.int "age" _.Age
            |> Schema.build

        let plan = compileFromSchema schema

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
        let schema =
            Schema.recordFor<Contact, _> (fun name age -> { Name = name; Age = age })
            |> Schema.text "name" _.Name
            |> Schema.int "age" _.Age
            |> Schema.build

        let plan = compileFromSchema schema

        raises<ArgumentException> <@ plan.Decode [| "name", "Ada" |] @>
