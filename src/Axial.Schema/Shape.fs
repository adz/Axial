namespace Axial.Schema

// FS0064: the SRTP witness pattern (`(^w or ^s) : ...` with ^w fixed to a concrete witness class)
// intentionally constrains the witness type variable; the warning is noise here.
#nowarn "64"

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

// The constructor-last authoring surface. An ObjectShape is a structural record shape that does not
// yet know how to construct its model: define + fields = structural shape; shape + constructor =
// schema. The shape's phantom parameters record the curried constructor type the declared fields
// demand, one argument per field, so any number of fields closes with a single `construct` call —
// there is no per-arity dispatch anywhere.

/// <summary>A typed field constraint: a <see cref="T:Axial.Schema.Constraint" /> that only applies to
/// fields whose value type is <typeparamref name="'value" />. Produced by the typed constraint
/// vocabulary in <see cref="T:Axial.Schema.Syntax" />.</summary>
[<Sealed>]
type Constraint<'value> internal (untyped: Constraint) =
    member internal _.Untyped = untyped

/// <summary>
/// An unfinished structural shape for <typeparamref name="'model" />: committed fields plus one current
/// field, but no constructor yet. The phantom parameters record the constructor the shape demands:
/// <typeparamref name="'constructor" /> is the full curried constructor type,
/// <typeparamref name="'remaining" /> is what is left of it after the declared fields consume their
/// arguments, and <typeparamref name="'last" /> is the current field's value type — the cursor that
/// <c>constrain</c> targets.
/// </summary>
/// <remarks>
/// <c>Schema.define</c> starts a definition, <c>field</c>/<c>fieldWith</c> add typed fields (committing
/// the previous one), <c>constrain</c> attaches a typed constraint to the current field, and
/// <c>construct</c>/<c>constructResult</c> commit the final field and close the shape into a
/// <see cref="T:Axial.Schema.Schema`1" /> by supplying the constructor last. Field count is unbounded:
/// each field peels one curried constructor argument by ordinary type inference.
/// </remarks>
[<Sealed>]
type ObjectShape<'model, 'constructor, 'remaining, 'last> internal (committed: obj, last: obj) =
    /// The committed chain: an IShapeFields&lt;'model,'constructor,'last -&gt; 'remaining&gt; over the fields
    /// declared before the current one.
    member internal _.Committed = committed
    /// The current field: a boxed FieldDefinition&lt;'model,'last&gt;.
    member internal _.Last = last

    /// <summary>Infrastructure for <c>field</c>/<c>fieldWith</c>; not intended for direct use.
    /// Commits the current field and installs the next one as the new cursor.</summary>
    static member Field
        (
            shape: ObjectShape<'model, 'constructor, 'field -> 'next, 'last>,
            name: string,
            getter: 'model -> 'field,
            valueSchema: Schema<'field>
        ) : ObjectShape<'model, 'constructor, 'next, 'field> =
        if isNull (box getter) then nullArg (nameof getter)
        if isNull (box valueSchema) then nullArg (nameof valueSchema)
        if isNull (box shape) then nullArg (nameof shape)

        let committed =
            ShapeFieldsAppend<'model, 'constructor, 'last, 'field -> 'next, _>(
                unbox<IShapeFields<'model, 'constructor, 'last -> 'field -> 'next>> shape.Committed,
                unbox<FieldDefinition<'model, 'last>> shape.Last
            )
            :> IShapeFields<'model, 'constructor, 'field -> 'next>

        let definition =
            { FieldDefinition.ExternalName = ExternalFieldName.create name
              Order = FieldOrder.create 0
              Getter = getter
              ValueSchema = valueSchema.ValueDefinition
              Constraints = [] }

        ObjectShape(box committed, box definition)

/// <summary>The starting point produced by <c>Schema.define</c>: a shape for
/// <typeparamref name="'model" /> with no fields declared yet. The first <c>field</c> or
/// <c>fieldWith</c> turns it into an <see cref="T:Axial.Schema.ObjectShape`4" />.</summary>
[<Sealed>]
type DefineShape<'model> internal () =

    /// <summary>Infrastructure for <c>field</c>/<c>fieldWith</c>; not intended for direct use.
    /// Declares the first field, fixing the shape's constructor type.</summary>
    static member Field
        (
            shape: DefineShape<'model>,
            name: string,
            getter: 'model -> 'field,
            valueSchema: Schema<'field>
        ) : ObjectShape<'model, 'field -> 'next, 'next, 'field> =
        if isNull (box getter) then nullArg (nameof getter)
        if isNull (box valueSchema) then nullArg (nameof valueSchema)
        if isNull (box shape) then nullArg (nameof shape)

        let committed =
            ShapeFieldsEmpty<'model, 'field -> 'next>() :> IShapeFields<'model, 'field -> 'next, 'field -> 'next>

        let definition =
            { FieldDefinition.ExternalName = ExternalFieldName.create name
              Order = FieldOrder.create 0
              Getter = getter
              ValueSchema = valueSchema.ValueDefinition
              Constraints = [] }

        ObjectShape(box committed, box definition)

[<RequireQualifiedAccess>]
module internal ShapeInternals =
    /// Commits the current field onto the typed chain, yielding the chain for every declared field.
    let commit
        (shape: ObjectShape<'model, 'constructor, 'remaining, 'last>)
        : IShapeFields<'model, 'constructor, 'remaining> =
        ShapeFieldsAppend<'model, 'constructor, 'last, 'remaining, _>(
            unbox<IShapeFields<'model, 'constructor, 'last -> 'remaining>> shape.Committed,
            unbox<FieldDefinition<'model, 'last>> shape.Last
        )
        :> IShapeFields<'model, 'constructor, 'remaining>

    let camelCase (name: string) =
        if System.String.IsNullOrEmpty name then
            name
        else
            string (System.Char.ToLowerInvariant name[0]) + name.Substring 1

/// <summary>
/// Canonical value-schema resolution for the inferred <c>field</c> form. A type participates by exposing
/// <c>static member Schema: T -&gt; Schema&lt;T&gt;</c>; Axial supplies that member for its supported built-in types.
/// When no member matches, the compile error points at this constraint: use <c>fieldWith</c> with an explicit schema.
/// </summary>
[<Sealed; AbstractClass>]
type SchemaDefaults =
    /// <summary>Builds an optional schema from an explicitly resolved item schema.</summary>
    static member OptionWith(item: Schema<'item>) : Schema<'item option> = SchemaCore.option item
    /// <summary>Builds a list schema from an explicitly resolved item schema.</summary>
    static member ListWith(item: Schema<'item>) : Schema<'item list> = SchemaCore.listWith item
    /// <summary>Builds a string-keyed map schema from an explicitly resolved value schema.</summary>
    static member MapWith(item: Schema<'item>) : Schema<Map<string, 'item>> = SchemaCore.mapWith item
    static member Schema(_: string) : Schema<string> = SchemaCore.text
    static member Schema(_: int) : Schema<int> = SchemaCore.``int``
    static member Schema(_: decimal) : Schema<decimal> = SchemaCore.``decimal``
    static member Schema(_: bool) : Schema<bool> = SchemaCore.``bool``
    static member Schema(_: System.DateTimeOffset) : Schema<System.DateTimeOffset> = SchemaCore.dateTime
    static member Schema(_: System.Guid) : Schema<System.Guid> = SchemaCore.guid
#if NET8_0_OR_GREATER
    static member Schema(_: System.DateOnly) : Schema<System.DateOnly> = SchemaCore.date
#endif

    static member inline Resolve() : Schema< ^value> =
        let inline call (witness: ^w, marker: ^v) : Schema< ^v> =
            ((^w or ^v): (static member Schema: ^v -> Schema< ^v>) marker)

        call (Unchecked.defaultof<SchemaDefaults>, Unchecked.defaultof< ^value>)

    static member inline Schema(_: ^item list) : Schema< ^item list> =
        SchemaDefaults.ListWith(SchemaDefaults.Resolve< ^item>())

    static member inline Schema(_: ^item option) : Schema< ^item option> =
        SchemaDefaults.OptionWith(SchemaDefaults.Resolve< ^item>())

    static member inline Schema(_: Map<string, ^item>) : Schema<Map<string, ^item>> =
        SchemaDefaults.MapWith(SchemaDefaults.Resolve< ^item>())

[<RequireQualifiedAccess>]
module internal ShapeOps =
    let define<'model> : DefineShape<'model> = DefineShape<'model>()

    /// Model-level trusted construction: maps a permissive draft schema to a domain schema through an
    /// admission function and a projection, preserving fields, wire names, constraints, and metadata.
    let admit (create: 'draft -> Result<'domain, string>) (project: 'domain -> 'draft) (draft: Schema<'draft>) : Schema<'domain> =
        if isNull (box create) then nullArg (nameof create)
        if isNull (box project) then nullArg (nameof project)
        if isNull (box draft) then nullArg (nameof draft)

        match draft.Definition with
        | ModelDefinition definition ->
            let fields =
                definition.Fields
                |> List.map (fun field ->
                    { FieldDescriptor.ExternalName = field.ExternalName
                      Order = field.Order
                      Getter = fun (domain: 'domain) -> field.Getter(project domain)
                      ValueSchema = field.ValueSchema
                      Constraints = field.Constraints })

            let tryApply arguments =
                definition.Constructor.TryApplyTrusted arguments |> Result.bind create

            let constructor =
                { ConstructorApplication.ArgumentCount = definition.Constructor.ArgumentCount
                  ApplyTrusted =
                    fun arguments ->
                        match tryApply arguments with
                        | Ok domain -> domain
                        | Error message -> invalidOp message
                  TryApplyTrusted = tryApply }

            Schema(
                ModelDefinition
                    { Constructor = constructor
                      Fields = fields
                      Description = definition.Description },
                None
            )
        | ValueDefinition _ ->
            invalidArg (nameof draft) "Schema.admit expects a model schema; refine value schemas with Schema.refine."
        | PendingDefinition -> invalidArg (nameof draft) "Expected a completed schema definition."

/// <summary>
/// The constructor-last schema authoring vocabulary: <c>field</c>, <c>fieldWith</c>, <c>constrain</c>,
/// typed constraints, and the closing <c>construct</c>/<c>constructResult</c>. Open this module locally
/// inside a schema-definition module; start shapes with <c>Schema.define</c>. To also use the bare
/// getter form (<c>field _.Name</c>), add <c>open type Axial.Schema.Syntax</c>.
/// </summary>
module Syntax =

    /// <summary>Adds a field using the supplied completed value schema. Prefer <c>field</c> when the field type has a
    /// canonical schema; use <c>fieldWith</c> for a local override, recursion, or a type that cannot contribute one.</summary>
    let inline fieldWith (valueSchema: Schema<'value>) (name: string) (getter: 'model -> 'value) (shape: ^shape) : ^shape' =
        (^shape: (static member Field: ^shape * string * ('model -> 'value) * Schema<'value> -> ^shape') (shape,
                                                                                                         name,
                                                                                                         getter,
                                                                                                         valueSchema))

    /// <summary>Adds a field whose value schema is inferred from the getter's result type. Supported types
    /// are the <see cref="T:Axial.Schema.SchemaDefaults" /> overload set plus any type exposing
    /// <c>static member Schema</c>. For anything else, use <c>fieldWith</c> with an explicit schema.</summary>
    let inline field (name: string) (getter: 'model -> ^value) (shape: ^shape) : ^shape' =
        fieldWith (SchemaDefaults.Resolve()) name getter shape

    /// <summary>Attaches a typed constraint to the current (most recently declared) field. The constraint's
    /// value type must match the field's value type, so a misplaced constraint fails to compile.</summary>
    let constrain
        (constraint': Constraint<'value>)
        (shape: ObjectShape<'model, 'constructor, 'remaining, 'value>)
        : ObjectShape<'model, 'constructor, 'remaining, 'value> =
        if isNull (box constraint') then nullArg (nameof constraint')
        if isNull (box shape) then nullArg (nameof shape)

        let definition = unbox<FieldDefinition<'model, 'value>> shape.Last

        ObjectShape(
            shape.Committed,
            box
                { definition with
                    Constraints = definition.Constraints @ [ constraint'.Untyped ] }
        )

    /// <summary>Adds a typed constraint to every item described by a list schema.</summary>
    let constrainItems (constraint': Constraint<'item>) (schema: Schema<'item list>) : Schema<'item list> =
        if isNull (box constraint') then nullArg (nameof constraint')
        SchemaCore.constrainItems constraint'.Untyped schema

    /// <summary>Adds a typed constraint to every value described by a string-keyed map schema.</summary>
    let constrainValues (constraint': Constraint<'item>) (schema: Schema<Map<string, 'item>>) : Schema<Map<string, 'item>> =
        if isNull (box constraint') then nullArg (nameof constraint')
        SchemaCore.constrainValues constraint'.Untyped schema

    /// <summary>Closes a shape with a total constructor. The constructor's curried parameters must match the
    /// declared fields in order and type; any number of fields is supported.</summary>
    let construct
        (f: 'constructor)
        (shape: ObjectShape<'model, 'constructor, 'model, 'last>)
        : Schema<'model> =
        if isNull (box f) then nullArg (nameof f)
        if isNull (box shape) then nullArg (nameof shape)
        SchemaCore.closeTotal (ShapeClosure(f, ShapeInternals.commit shape))

    /// <summary>Closes a shape with a checked constructor returning <c>Result&lt;'model, string&gt;</c>. The
    /// error becomes a schema diagnostic; interpreters place it with <c>Schema.constructorErrorAt</c>.</summary>
    let constructResult
        (f: 'constructor)
        (shape: ObjectShape<'model, 'constructor, Result<'model, string>, 'last>)
        : Schema<'model> =
        if isNull (box f) then nullArg (nameof f)
        if isNull (box shape) then nullArg (nameof shape)
        SchemaCore.closeResult (ShapeClosure(f, ShapeInternals.commit shape))

    // ---- typed constraints ----

    /// <summary>Requires a text field to have at least the supplied length.</summary>
    let minLength minimum : Constraint<string> = Constraint<string>(Constraint.minLength minimum)

    /// <summary>Requires a text field to have at most the supplied length.</summary>
    let maxLength maximum : Constraint<string> = Constraint<string>(Constraint.maxLength maximum)

    /// <summary>Requires a text field's length to fall inside the supplied inclusive bounds.</summary>
    let lengthBetween minimum maximum : Constraint<string> =
        Constraint<string>(Constraint.lengthBetween minimum maximum)

    /// <summary>Requires a text field to match Axial's pragmatic email format.</summary>
    let emailFormat: Constraint<string> = Constraint<string> Constraint.email

    /// <summary>Requires a text field to have no leading or trailing whitespace.</summary>
    let trimmed: Constraint<string> = Constraint<string> Constraint.trimmed

    /// <summary>Requires a text field to match the supplied regular expression pattern.</summary>
    let pattern expression : Constraint<string> = Constraint<string>(Constraint.pattern expression)

    /// <summary>Requires a field to be at least the supplied value (inclusive).</summary>
    let atLeast (minimum: 'value) : Constraint<'value> = Constraint<'value>(Constraint.atLeast minimum)

    /// <summary>Requires a field to be at most the supplied value (inclusive).</summary>
    let atMost (maximum: 'value) : Constraint<'value> = Constraint<'value>(Constraint.atMost maximum)

    /// <summary>Requires a field to be greater than the supplied value (exclusive).</summary>
    let greaterThan (minimum: 'value) : Constraint<'value> = Constraint<'value>(Constraint.greaterThan minimum)

    /// <summary>Requires a field to be less than the supplied value (exclusive).</summary>
    let lessThan (maximum: 'value) : Constraint<'value> = Constraint<'value>(Constraint.lessThan maximum)

    /// <summary>Requires a numeric field to be an exact multiple of the supplied value.</summary>
    let multipleOf (factor: 'value) : Constraint<'value> = Constraint<'value>(Constraint.multipleOf factor)

    /// <summary>Requires a field to fall inside the supplied inclusive bounds.</summary>
    let between (minimum: 'value) (maximum: 'value) : Constraint<'value> =
        Constraint<'value>(Constraint.between minimum maximum)

    /// <summary>Requires a field to differ from the supplied value.</summary>
    let notEqualTo (unexpected: 'value) : Constraint<'value> = Constraint<'value>(Constraint.notEqualTo unexpected)

    /// <summary>Requires a text field to equal one of the supplied choices.</summary>
    let oneOf (choices: string list) : Constraint<string> = Constraint<string>(Constraint.oneOf choices)

    /// <summary>Requires a list field to have exactly the supplied number of items.</summary>
    let count expected : Constraint<'item list> = Constraint<'item list>(Constraint.count expected)

    /// <summary>Requires a list field to have at least the supplied number of items.</summary>
    let minCount minimum : Constraint<'item list> = Constraint<'item list>(Constraint.minCount minimum)

    /// <summary>Requires a list field to have at most the supplied number of items.</summary>
    let maxCount maximum : Constraint<'item list> = Constraint<'item list>(Constraint.maxCount maximum)

    /// <summary>Requires a list field's item count to fall inside the supplied inclusive bounds.</summary>
    let countBetween minimum maximum : Constraint<'item list> =
        Constraint<'item list>(Constraint.countBetween minimum maximum)

    /// <summary>Requires a list field's items to be distinct.</summary>
    let distinct<'item when 'item: equality> : Constraint<'item list> = Constraint<'item list> Constraint.distinct

    /// <summary>Requires a list field to contain the supplied item.</summary>
    let contains (item: 'item) : Constraint<'item list> = Constraint<'item list>(Constraint.contains item)

    /// <summary>Replaces a typed constraint's user-facing message.</summary>
    let withMessage (message: string) (constraint': Constraint<'value>) : Constraint<'value> =
        Constraint<'value>(Constraint.withMessage message constraint'.Untyped)

/// <summary>
/// The bare-getter field form: <c>open type Axial.Schema.Syntax</c> brings an overloaded <c>field</c>
/// into scope that accepts either a name and getter (like the module form) or a bare property getter
/// such as <c>field _.Name</c>, deriving the wire name from the property (camelCased). Explicit names
/// are never transformed; the camelCase policy applies only to derived names.
/// </summary>
[<Sealed; AbstractClass>]
type Syntax =

    /// <summary>Splits a property-access getter quotation into a derived (camelCased) wire name and the
    /// compiled getter. Infrastructure for the bare <c>field</c> form; not intended for direct use.</summary>
    static member DerivedField(getter: Expr<'model -> 'value>) : string * ('model -> 'value) =
        match getter :> Expr with
        | WithValue(value, _, Lambda(_, PropertyGet(Some(Var _), property, []))) ->
            ShapeInternals.camelCase property.Name, (value :?> ('model -> 'value))
        | _ ->
            invalidArg
                (nameof getter)
                "The bare field form requires a property getter such as `_.Name`; use `field \"name\" getter` for anything else."

    /// <summary>Adds a field from a bare property getter such as <c>field _.Name</c>: the wire name is the
    /// camelCased property name and the value schema is inferred like the named <c>field</c> form.</summary>
    static member inline field([<ReflectedDefinition(includeValue = true)>] getter: Expr<'model -> ^value>) : ^shape -> ^shape' =
        let name, get = Syntax.DerivedField getter
        let valueSchema: Schema< ^value> = SchemaDefaults.Resolve()

        fun shape ->
            (^shape: (static member Field: ^shape * string * ('model -> ^value) * Schema< ^value> -> ^shape') (shape,
                                                                                                              name,
                                                                                                              get,
                                                                                                              valueSchema))

    /// <summary>Adds a field with an explicit wire name and inferred value schema; identical to the module-level
    /// <c>field</c>, provided here so <c>open type</c> users keep the named form under the same word.</summary>
    static member inline field(name: string) : (('model -> ^value) -> ^shape -> ^shape') =
        fun getter shape ->
            let valueSchema: Schema< ^value> = SchemaDefaults.Resolve()

            (^shape: (static member Field: ^shape * string * ('model -> ^value) * Schema< ^value> -> ^shape') (shape,
                                                                                                              name,
                                                                                                              getter,
                                                                                                              valueSchema))
