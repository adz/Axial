// Interpreter-facing typed record-plan contracts. SchemaBuilder.fs retains the typed field chain
// and compiles it through these contracts without reflection or obj-array constructor dispatch.
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

type internal ICompiledRecordPlan<'model> =
    abstract member CompilePlan<'result> : factory: IRecordPlanCompiler<'model, 'result> -> 'result

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
