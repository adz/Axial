// The typed record plan retained when a constructor-last shape closes: IShapeFields is the typed
// field chain (empty + append nodes, one curried constructor argument peeled per field),
// ShapeClosure pairs a chain with its constructor, and IRecordPlanCompiler/CompiledRecordPlan let
// interpreters such as Axial.Schema.Json fold the chain into direct typed encode/decode plans
// without reconstructing types. Shape.fs builds these; interpreters consume them.
namespace Axial.Schema

open System
open System.Collections.Generic

/// <summary>Holds an interpreter-specific typed record-plan fragment while compiling a schema.</summary>
/// <remarks>
/// Schema interpreters use this as the typed accumulator returned by
/// <see cref="T:Axial.Schema.IRecordPlanCompiler`2" />. The value is intentionally opaque to <c>Axial.Schema</c>;
/// each interpreter owns the concrete plan shape it stores here.
/// </remarks>
type IRecordPlanState<'model, 'constructorIn, 'constructorOut> =
    /// <summary>Gets the interpreter-owned typed record-plan fragment.</summary>
    abstract member Value: obj

/// <summary>
/// Builds an interpreter-specific typed record plan from a model schema's authored shape.
/// </summary>
/// <remarks>
/// The built <see cref="T:Axial.Schema.Schema`1" /> keeps this typed shape alongside its type-erased
/// <c>FieldDescriptor</c> metadata. Interpreters that need constructor-specialized plans, such as codecs, can walk the
/// record plan through this compiler without asking callers to re-supply fields or constructors and without lowering
/// construction to <c>obj array</c> dispatch.
/// </remarks>
type IRecordPlanCompiler<'model, 'result> =
    /// <summary>Starts a record plan for a constructor with no consumed fields.</summary>
    abstract member OnEnd<'constructor> : unit -> IRecordPlanState<'model, 'constructor, 'constructor>

    /// <summary>Appends one typed field to an interpreter-specific record plan.</summary>
    abstract member OnField<'constructorIn, 'field, 'next> :
        order: int *
        field: Field<'model, 'field> *
        head: IRecordPlanState<'model, 'constructorIn, 'field -> 'next> ->
            IRecordPlanState<'model, 'constructorIn, 'next>

    /// <summary>Completes a record plan with the original typed constructor.</summary>
    abstract member OnComplete<'constructor, 'constructed> :
        constructor: 'constructor *
        plan: IRecordPlanState<'model, 'constructor, 'constructed> *
        finish: ('constructed -> Result<'model, string>) -> 'result

type IShapeFields<'model, 'constructor, 'remaining> =
    abstract member GetFields: int -> obj list * int
    abstract member Apply: constructor: obj * arguments: obj array -> obj
    abstract member Build<'result> :
        factory: IRecordPlanCompiler<'model, 'result> -> IRecordPlanState<'model, 'constructor, 'remaining>

type internal ShapeFieldsEmpty<'model, 'constructor>() =
    interface IShapeFields<'model, 'constructor, 'constructor> with
        member _.GetFields(index) = [], index
        member _.Apply(constructor, _) = constructor
        member _.Build(factory) = factory.OnEnd()

type internal ShapeFieldsAppend<'model, 'constructor, 'field, 'next, 'head
    when 'head :> IShapeFields<'model, 'constructor, 'field -> 'next>>
    internal
    (
        head: 'head,
        field: FieldDefinition<'model, 'field>
    ) =

    interface IShapeFields<'model, 'constructor, 'next> with
        member _.GetFields(index) =
            let fields, nextIndex = (head :> IShapeFields<'model, 'constructor, 'field -> 'next>).GetFields index

            let descriptor =
                { FieldDescriptor.ExternalName = field.ExternalName
                  Order = FieldOrder.create nextIndex
                  Getter = fun model -> field.Getter model |> box
                  ValueSchema = field.ValueSchema
                  Constraints = field.Constraints }

            fields @ [ box descriptor ], nextIndex + 1

        member _.Apply(constructor, arguments) =
            let fieldIndex = (head :> IShapeFields<'model, 'constructor, 'field -> 'next>).GetFields(0) |> snd
            let appliedHead =
                (head :> IShapeFields<'model, 'constructor, 'field -> 'next>).Apply(constructor, arguments)

            let typedConstructor = unbox<'field -> 'next> appliedHead
            typedConstructor (unbox<'field> arguments[fieldIndex]) |> box

        member _.Build(factory) =
            let headNode = head :> IShapeFields<'model, 'constructor, 'field -> 'next>
            let headResult = headNode.Build(factory)
            let order = headNode.GetFields(0) |> snd

            let typedField =
                Field(
                    { field with
                        Order = FieldOrder.create order }
                )

            factory.OnField(order, typedField, headResult)

/// <summary>
/// Internal closing state that combines a constructor with the typed fields recovered from an object shape.
/// </summary>
/// <remarks>
/// Constructor-last arity dispatch creates this value after matching the shape's phantom field types against the
/// constructor. It is not an authoring surface; it carries the typed plan into compiled interpreters.
/// </remarks>
type internal ShapeClosure<'model, 'constructor, 'remaining, 'chain
    when 'chain :> IShapeFields<'model, 'constructor, 'remaining>>
    internal
    (
        constructor: 'constructor,
        fields: 'chain
    ) =
    member internal _.Constructor = constructor
    member internal _.Fields = fields

type internal ICompiledRecordPlan<'model> =
    abstract member CompilePlan<'result> : factory: IRecordPlanCompiler<'model, 'result> -> 'result

type internal CompiledRecordPlan<'model, 'constructor, 'constructed, 'fields
    when 'fields :> IShapeFields<'model, 'constructor, 'constructed>>
    (
        constructor: 'constructor,
        fields: 'fields,
        finish: 'constructed -> Result<'model, string>
    ) =

    interface ICompiledRecordPlan<'model> with
        member _.CompilePlan(factory) =
            if isNull (box factory) then
                nullArg (nameof factory)

            let result = fields.Build(factory)
            factory.OnComplete(constructor, result, finish)

module internal ModelSchemaDefinition =
    let private ensureContiguousOrders (fields: FieldDescriptor<'model> list) =
        let orders = fields |> List.map (fun field -> field.Order.Value) |> List.sort

        let expected = [ 0 .. (List.length fields - 1) ]

        if orders <> expected then
            invalidArg (nameof fields) "Model schema fields must use contiguous zero-based field order."

    let create (constructor: ConstructorApplication<'model>) (fields: FieldDescriptor<'model> list) =
        if isNull (box constructor) then
            nullArg (nameof constructor)

        if isNull (box fields) then
            nullArg (nameof fields)

        fields
        |> List.iter (fun field ->
            if isNull (box field) then
                nullArg (nameof fields))

        if fields.Length <> constructor.ArgumentCount then
            invalidArg
                (nameof fields)
                $"Expected {constructor.ArgumentCount} ordered field(s), but received {fields.Length}."

        ensureContiguousOrders fields

        fields
        |> List.iter (fun field ->
            match field.ValueSchema.Shape with
            | OptionValueDefinition _ when
                field.Constraints
                |> List.exists (fun constraint' -> constraint'.Code = "required")
                ->
                invalidArg (nameof fields) "Optional fields cannot carry the required constraint."
            | _ -> ())

        { Constructor = constructor
          Fields = fields |> List.sortBy (fun field -> field.Order.Value)
          Description = None }
