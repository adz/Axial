// The record-schema computation expression. The outer builder separates fields and retains a typed
// constructor chain; the optional inner field builder transforms one Schema<_> value.
namespace Axial.Schema

#nowarn "64"

open System.ComponentModel
open Axial.Refined

#if !FABLE_COMPILER
open Microsoft.FSharp.Quotations
#endif

[<EditorBrowsable(EditorBrowsableState.Never)>]
type FieldInitial<'model, 'target> internal (name: string, getter: 'model -> 'target) =
    member internal _.Name = name
    member internal _.Getter = getter

[<EditorBrowsable(EditorBrowsableState.Never)>]
type FieldWorking<'model, 'target, 'current> internal
    (
        initial: FieldInitial<'model, 'target>,
        schema: Schema<'current>
    ) =
    member internal _.Initial = initial
    member internal _.Schema = schema

[<EditorBrowsable(EditorBrowsableState.Never)>]
type FieldRefining<'model, 'target, 'raw> internal
    (
        initial: FieldInitial<'model, 'target>,
        rawSchema: Schema<'raw>,
        validations: ('target -> Result<unit, SchemaError>) list
    ) =
    member internal _.Initial = initial
    member internal _.RawSchema = rawSchema
    member internal _.Validations = validations

[<EditorBrowsable(EditorBrowsableState.Never)>]
type FieldDeclaration<'model, 'value> internal (definition: FieldDefinition<'model, 'value>) =
    member internal _.Definition = definition

[<EditorBrowsable(EditorBrowsableState.Never)>]
type RefiningFieldDeclaration<'model, 'raw, 'target> internal
    (
        initial: FieldInitial<'model, 'target>,
        rawSchema: Schema<'raw>,
        validations: ('target -> Result<unit, SchemaError>) list
    ) =
    member internal _.Initial = initial
    member internal _.RawSchema = rawSchema
    member internal _.Validations = validations

/// <summary>Configures one field inside <c>schema&lt;'model&gt; { }</c>.</summary>
/// <exclude />
[<EditorBrowsable(EditorBrowsableState.Never)>]
type FieldBuilder<'model, 'target> internal (name: string, getter: 'model -> 'target) =
    member internal _.Name = name
    member internal _.Getter = getter

    member _.Yield(()) : FieldInitial<'model, 'target> =
        FieldInitial(name, getter)

    /// <summary>Supplies the schema transformed by the remaining operations in this field block.</summary>
    [<CustomOperation("withSchema")>]
    member _.WithSchema
        (
            initial: FieldInitial<'model, 'target>,
            schema: Schema<'current>
        ) : FieldWorking<'model, 'target, 'current> =
        if isNull (box schema) then nullArg (nameof schema)
        FieldWorking(initial, schema)

    /// <summary>Adds a portable constraint to the field's current schema value.</summary>
    [<CustomOperation("constrain")>]
    member _.Constrain
        (
            field: FieldWorking<'model, 'target, 'current>,
            constraint': Constraint<'current>
        ) : FieldWorking<'model, 'target, 'current> =
        if isNull (box constraint') then nullArg (nameof constraint')
        FieldWorking(field.Initial, field.Schema |> SchemaCore.constrain constraint'.Untyped)

    /// <summary>Refines the current raw schema into the field getter's result type.</summary>
    [<CustomOperation("refine")>]
    member _.Refine
        (field: FieldWorking<'model, 'target, 'raw>)
        : FieldRefining<'model, 'target, 'raw> =
        FieldRefining(field.Initial, field.Schema, [])

    /// <summary>Adds executable validation to the field's current schema value.</summary>
    [<CustomOperation("validate")>]
    member _.Validate
        (
            field: FieldWorking<'model, 'target, 'current>,
            validation: 'current -> Result<unit, SchemaError>
        ) : FieldWorking<'model, 'target, 'current> =
        FieldWorking(field.Initial, field.Schema |> SchemaCore.validate validation)

    /// <summary>Adds executable validation after the pending refinement.</summary>
    [<CustomOperation("validate")>]
    member _.Validate
        (
            field: FieldRefining<'model, 'target, 'raw>,
            validation: 'target -> Result<unit, SchemaError>
        ) : FieldRefining<'model, 'target, 'raw> =
        FieldRefining(field.Initial, field.RawSchema, field.Validations @ [ validation ])

    member _.Run
        (field: FieldWorking<'model, 'target, 'target>)
        : FieldDeclaration<'model, 'target> =
        FieldDeclaration(
            { ExternalName = ExternalFieldName.create field.Initial.Name
              Order = FieldOrder.create 0
              Getter = field.Initial.Getter
              ValueSchema = field.Schema.ValueDefinition
              Constraints = [] }
        )

    member _.Run
        (field: FieldRefining<'model, 'target, 'raw>)
        : RefiningFieldDeclaration<'model, 'raw, 'target> =
        RefiningFieldDeclaration(field.Initial, field.RawSchema, field.Validations)

    [<CompilerMessage(
        "A field block must finish with the getter type. Add `refine` after raw-schema operations.",
        12001,
        IsError = true
    )>]
    member _.Run
        (field: FieldWorking<'model, 'target, 'current>)
        : FieldDeclaration<'model, 'target> =
        invalidOp $"Field '{field.Initial.Name}' has an unfinished raw schema."

[<EditorBrowsable(EditorBrowsableState.Never)>]
type FieldStep<'model, 'value> internal (definition: FieldDefinition<'model, 'value>) =
    member internal _.Definition = definition

[<EditorBrowsable(EditorBrowsableState.Never)>]
type ConstructorStep<'model, 'constructor> internal (constructor: 'constructor) =
    member internal _.Constructor = constructor

[<EditorBrowsable(EditorBrowsableState.Never)>]
type CheckedConstructorStep<'model, 'constructor> internal (constructor: 'constructor) =
    member internal _.Constructor = constructor

type internal ICeFields<'model, 'remaining, 'constructed> =
    abstract member GetFields: int -> obj list * int
    abstract member Apply: 'remaining * obj array * int -> 'constructed
    abstract member Build<'constructor, 'result> :
        factory: IRecordPlanCompiler<'model, 'result> *
        state: IRecordPlanState<'model, 'constructor, 'remaining> *
        order: int ->
            IRecordPlanState<'model, 'constructor, 'constructed> * int

type internal CeFieldsEmpty<'model, 'constructed>() =
    interface ICeFields<'model, 'constructed, 'constructed> with
        member _.GetFields(index) = [], index
        member _.Apply(constructed, _, _) = constructed
        member _.Build(_, state, order) = state, order

type internal CeFieldsCons<'model, 'field, 'tail, 'constructed>
    (
        field: FieldDefinition<'model, 'field>,
        tail: ICeFields<'model, 'tail, 'constructed>
    ) =
    interface ICeFields<'model, 'field -> 'tail, 'constructed> with
        member _.GetFields(index) =
            let descriptor: FieldDescriptor<'model> =
                { ExternalName = field.ExternalName
                  Order = FieldOrder.create index
                  Getter = fun model -> field.Getter model |> box
                  ValueSchema = field.ValueSchema
                  Constraints = field.Constraints }

            let rest, next = tail.GetFields(index + 1)
            box descriptor :: rest, next

        member _.Apply(constructor, arguments, index) =
            let next = constructor (unbox<'field> arguments[index])
            tail.Apply(next, arguments, index + 1)

        member _.Build(factory, state, order) =
            let typedField =
                Field(
                    { field with
                        Order = FieldOrder.create order }
                )

            let next = factory.OnField(order, typedField, state)
            tail.Build(factory, next, order + 1)

[<EditorBrowsable(EditorBrowsableState.Never)>]
type SchemaPlan<'model, 'expected, 'constructed, 'actual> internal
    (
        fields: obj,
        constructor: 'actual,
        finish: 'constructed -> Result<'model, string>
    ) =
    member internal _.Fields = fields
    member internal _.Constructor = constructor
    member internal _.Finish = finish

type internal CeCompiledRecordPlan<'model, 'constructor, 'constructed>
    (
        constructor: 'constructor,
        fields: ICeFields<'model, 'constructor, 'constructed>,
        finish: 'constructed -> Result<'model, string>
    ) =
    interface ICompiledRecordPlan<'model> with
        member _.CompilePlan(factory) =
            let initial = factory.OnEnd<'constructor>()
            let completed, _ = fields.Build(factory, initial, 0)
            factory.OnComplete(constructor, completed, finish)

[<RequireQualifiedAccess>]
module internal SchemaBuilderInternals =
    let close
        (constructor: 'constructor)
        (fields: ICeFields<'model, 'constructor, 'constructed>)
        (finish: 'constructed -> Result<'model, string>)
        : Schema<'model> =
        let descriptors, count =
            fields.GetFields 0
            |> fun (values, count) ->
                values |> List.map unbox<FieldDescriptor<'model>>, count

        let tryApply arguments =
            ConstructorApplication.ensureArgumentCount count arguments
            fields.Apply(constructor, arguments, 0) |> finish

        let application =
            { ArgumentCount = count
              ApplyTrusted =
                fun arguments ->
                    match tryApply arguments with
                    | Ok model -> model
                    | Error message -> invalidOp message
              TryApplyTrusted = tryApply }

        let compiled =
            CeCompiledRecordPlan<'model, 'constructor, 'constructed>(constructor, fields, finish)
            :> ICompiledRecordPlan<'model>

        Schema(ModelDefinition(ModelSchemaDefinition.create application descriptors), Some compiled)

/// <summary>Builds a typed record schema from ordered field declarations and a final constructor.</summary>
/// <exclude />
[<EditorBrowsable(EditorBrowsableState.Never)>]
type SchemaBuilder<'model>() =
    static member DefaultField
        (
            field: FieldBuilder<'model, 'value>,
            schema: Schema<'value>
        ) : FieldStep<'model, 'value> =
        FieldStep(
            { ExternalName = ExternalFieldName.create field.Name
              Order = FieldOrder.create 0
              Getter = field.Getter
              ValueSchema = schema.ValueDefinition
              Constraints = [] }
        )

    static member RefinedField
        (
            field: RefiningFieldDeclaration<'model, 'raw, 'target>,
            refinement: Refinement<'raw, 'target>
        ) : FieldStep<'model, 'target> =
        let schema =
            field.Validations
            |> List.fold
                (fun current validation -> SchemaCore.validate validation current)
                (field.RawSchema |> SchemaCore.refine refinement)

        FieldStep(
            { ExternalName = ExternalFieldName.create field.Initial.Name
              Order = FieldOrder.create 0
              Getter = field.Initial.Getter
              ValueSchema = schema.ValueDefinition
              Constraints = [] }
        )

    member inline _.Yield(field: FieldBuilder<'model, ^value>) : FieldStep<'model, ^value> =
        let schema: Schema< ^value> = SchemaDefaults.Resolve()
        SchemaBuilder<'model>.DefaultField(field, schema)

    member _.Yield(field: FieldDeclaration<'model, 'value>) =
        FieldStep(field.Definition)

    member inline _.Yield
        (field: RefiningFieldDeclaration<'model, ^raw, ^target>)
        : FieldStep<'model, ^target> =
        let refinement: Refinement<^raw, ^target> = RefinementFrom.Resolve()
        SchemaBuilder<'model>.RefinedField(field, refinement)

    member _.Yield(step: ConstructorStep<'model, 'constructor>) =
        SchemaPlan<'model, 'model, 'model, 'constructor>(
            box (CeFieldsEmpty<'model, 'model>() :> ICeFields<'model, 'model, 'model>),
            step.Constructor,
            Ok
        )

    member _.Yield(step: CheckedConstructorStep<'model, 'constructor>) =
        SchemaPlan<'model, Result<'model, string>, Result<'model, string>, 'constructor>(
            box (
                CeFieldsEmpty<'model, Result<'model, string>>()
                :> ICeFields<'model, Result<'model, string>, Result<'model, string>>
            ),
            step.Constructor,
            id
        )

    member _.Combine
        (
            field: FieldStep<'model, 'value>,
            plan: SchemaPlan<'model, 'tail, 'constructed, 'constructor>
        ) =
        let tail =
            unbox<ICeFields<'model, 'tail, 'constructed>> plan.Fields

        let fields =
            CeFieldsCons<'model, 'value, 'tail, 'constructed>(field.Definition, tail)
            :> ICeFields<'model, 'value -> 'tail, 'constructed>

        SchemaPlan<'model, 'value -> 'tail, 'constructed, 'constructor>(
            box fields,
            plan.Constructor,
            plan.Finish
        )

    member _.Delay(factory: unit -> 'state) =
        factory()

    member _.Run
        (plan: SchemaPlan<'model, 'constructor, 'constructed, 'constructor>)
        : Schema<'model> =
        let fields =
            unbox<ICeFields<'model, 'constructor, 'constructed>> plan.Fields

        SchemaBuilderInternals.close plan.Constructor fields plan.Finish

/// <summary>Record-schema computation-expression vocabulary.</summary>
module SchemaCE =
    /// <summary>Record-schema computation expression.</summary>
    let schema<'model> = SchemaBuilder<'model>()

    /// <summary>Declares a field with an explicit wire name.</summary>
    let field (name: string) (getter: 'model -> 'value) =
        if isNull name then nullArg (nameof name)
        if isNull (box getter) then nullArg (nameof getter)
        FieldBuilder<'model, 'value>(name, getter)

    /// <summary>Closes a record schema with a total constructor.</summary>
    let construct<'model, 'constructor> (constructor: 'constructor) =
        if isNull (box constructor) then nullArg (nameof constructor)
        ConstructorStep<'model, 'constructor>(constructor)

    /// <summary>Closes a record schema with a checked constructor.</summary>
    let constructResult<'model, 'constructor> (constructor: 'constructor) =
        if isNull (box constructor) then nullArg (nameof constructor)
        CheckedConstructorStep<'model, 'constructor>(constructor)

#if !FABLE_COMPILER
    /// <summary>Declares a field from a bare property getter and derives its camel-cased wire name.</summary>
    let fieldFromGetter
        ([<ReflectedDefinition(includeValue = true)>] getter: Expr<'model -> 'value>)
        =
        let name, get = Syntax.DerivedField getter
        FieldBuilder<'model, 'value>(name, get)
#endif
